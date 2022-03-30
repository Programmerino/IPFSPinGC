module IPFSPinGC

open FSharpPlus
open FSharp.Data
open System
open pinAPI
open pinAPI.Types
open Argu
open System.Net.Http
open FSharp.Control.Reactive
open System.Threading

type userPinnedDataTotalResponse = JsonProvider<"./JSON/userPinnedDataTotalResponse.json", InferTypesFromValues = true>
type pinListResponse = JsonProvider<"./JSON/pinListResponse.json", InferTypesFromValues = true>

type Sort =
    | Size
    | Added

type CliArguments =
    | [<AltCommandLine("-p")>] Pinata
    | [<AltCommandLine("-t")>] AccessToken of token:string
    | SortMethod of Sort
    | [<AltCommandLine("-g")>] Gateway of URL:string
    | [<AltCommandLine("-d")>] Dry
    | [<AltCommandLine("-a")>] APIUrl of URL:string
    | [<MainCommand; ExactlyOnce; First>] SizeLimit of size:uint64


    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | APIUrl _ -> "specify the API url of the target pinning service (mandatory if pinata is not enabled)"
            | Pinata _ -> "use pinata to get the total pinned data size rather than counting all pins (more efficient if actually using pinata, also sets defaults)"
            | AccessToken _ -> "specify the access token of your account (for pinata, see https://pinata.cloud/keys and use the JWT)"
            | SortMethod _ -> "specify the sort method to use (default is removing oldest pins). Size takes longer because it needs to query each CID to find its size"
            | Gateway _ -> "specify the gateway to use for size checking (default is https://localhost:8080, if pinata is specified it is the pinata gateway)"
            | Dry _ -> "dry run; do not actually delete any pins"
            | SizeLimit _ -> "specify the size limit to garbage collect down to in megabytes"

let http = new HttpClient()

let getSize (cid: string) (gateway: string) =
    task {
        let request = new HttpRequestMessage(Net.Http.HttpMethod.Head, $"{gateway}/ipfs/{cid}")

        use! response = http.SendAsync(request)

        return match response.Content.Headers.ContentLength |> Option.ofNullable with
                        | Some size -> int64(size)
                        | None -> failwith "The gateway provided does not support HEAD or does not return Content-Length. Try another"
    } |> Async.AwaitTask
    
let remove (client: pinAPIClient) dry x =
    let mutable first = true
    if (not dry) then
        x
        |> Seq.chunkBySize 30
        |> Seq.bind (fun x ->
            if (not first) then printfn "Rate limited, waiting... "; Thread.Sleep(45000); printfn "Continuing..."
            first <- false

            x |> Seq.map(fun pObj -> Thread.Sleep(500); pObj.pin.cid, client.DeletePinsByRequestid pObj.requestid |> Async.RunSynchronously)
        )
        |> Seq.iter(fun (cid, y) -> printfn $"Removed {cid}: {y}")
    else
        x
        |> Seq.iter(fun x -> printfn $"Removed {x.pin.cid}")

let GCPinata (client: pinAPIClient) jwt sort (size: uint64) dry =
    task {
        let desiredBytes = int(size) * 1000000

        use http = new HttpClient()
        http.DefaultRequestHeaders.Authorization <- new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt)

        let! respObj = http.GetAsync("https://api.pinata.cloud/data/userPinnedDataTotal")
        let! content = respObj.Content.ReadAsStringAsync()

        let resp = userPinnedDataTotalResponse.Parse(content)

        let totalSize = resp.PinSizeTotal

        let! respObj = http.GetAsync("https://api.pinata.cloud/data/pinList?status=pinned&pageLimit=1000")
        let! content = respObj.Content.ReadAsStringAsync()

        let resp = pinListResponse.Parse(content)

        let toRemove = match sort with
                        | Added ->
                            resp.Rows
                            |> Array.sortBy(fun x -> x.DatePinned)
                        | Size ->
                            resp.Rows
                            |> Array.sortByDescending(fun x -> x.Size)

                        |> Array.scan (fun (accumS, accumR) x ->
                            (accumS - x.Size, x :: accumR)
                        ) (totalSize, List.empty)
                        |> Array.takeWhile (fun (accumS, _) -> accumS > desiredBytes)
                        |> Array.last
                        |> snd

        let mutable first = true;

        let pinObjs =
            toRemove
            |> List.map(fun x -> x.IpfsPinHash)
            |> List.chunkBySize 10
            |> Seq.chunkBySize 6
            |> Seq.bind (fun x ->
                if (not first) then printfn "Rate limited, waiting... "; Thread.Sleep(45000); printfn "Continuing..."
                first <- false
                x
                |> Seq.map(fun x -> Thread.Sleep(500); client.GetPins(cid = x) |> Async.RunSynchronously)
                |> Seq.map (fun x ->
                    match x with
                    | GetPins.OK x -> Ok x.results
                    | GetPins.BadRequest x -> Error $"Error getting pins: {x}"
                    | GetPins.NotFound x -> Error $"Error getting pins: {x}"
                    | GetPins.Unauthorized x -> Error $"Error getting pins: {x}"
                    | GetPins.Conflict x -> Error $"Error getting pins: {x}"
                )
                |> Seq.choose(Result.mapError(fun x -> printfn $"{x}") >> Option.ofResult)
                |> Seq.bind (List.toSeq >> id)
            )

        remove client dry pinObjs
    }
    |> Async.AwaitTask

let GCPinningApi (client: pinAPIClient) sort (size: uint64) gateway dry = async {
    let! pins = client.GetPins(limit = 1000)
    match pins with
    | GetPins.OK x ->

        let sizedPins =
            x.results
            |> Observable.ofSeq
            |> Observable.flatmap(fun x -> getSize x.pin.cid gateway |> Observable.ofAsync |> Observable.map(fun y -> x, y))
            |> Observable.toArray
            |> Observable.wait

        let totalSize = sizedPins |> Array.sumBy(snd)

        let desiredBytes = int64(size) * 1000000L

        let sortMethod =
            match sort with
            | Added -> Array.sortBy(fun (x, _) -> x.created)
            | Size -> Array.sortByDescending(fun (_, x) -> x)

        sizedPins
        |> sortMethod
        |> Array.scan (fun (accumS, accumR) (x, size) ->
            (accumS - size, x :: accumR)
        ) (totalSize, List.empty)
        |> Array.takeWhile (fun (accumS, _) -> accumS > desiredBytes)
        |> Array.last
        |> snd
        |> remove client dry
    | GetPins.BadRequest x -> failwith $"Error getting pins: {x}"
    | GetPins.NotFound x -> failwith $"Error getting pins: {x}"
    | GetPins.Unauthorized x -> failwith $"Error getting pins: {x}"
    | GetPins.Conflict x -> failwith $"Error getting pins: {x}"
}

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<CliArguments>(programName = "IPFSPinGC")
    let results = parser.Parse(argv)
    let pinata = results.Contains(Pinata)
    let apiurl =
        match results.TryGetResult(APIUrl) with
        | Some(x) -> x
        | None -> if pinata then "https://api.pinata.cloud/psa" else failwith "You must specify the API URL if you aren't using Pinata"
    let accesstoken =
        match results.TryGetResult(AccessToken) with
        | Some(x) -> x
        | None -> if pinata then failwith "You must specify the access token if you are using Pinata" else ""
    let sort = results.GetResult(SortMethod, defaultValue = Added)
    let size = results.GetResult(SizeLimit)
    let gateway = results.GetResult(Gateway, defaultValue = if pinata then "https://gateway.pinata.cloud" else "https://localhost:8080")
    let dry = results.Contains(Dry)

    let httpClient = new HttpClient(BaseAddress=Uri(apiurl))
    if accesstoken <> "" then httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accesstoken}")
    let pinAPI = pinAPIClient httpClient

    if pinata then
        GCPinata pinAPI accesstoken sort size dry
    else
        GCPinningApi pinAPI sort size gateway dry
    |> Async.RunSynchronously

    0
