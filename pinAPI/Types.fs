namespace rec pinAPI.Types

///Status a pin object can have at a pinning service
[<Fable.Core.StringEnum; RequireQualifiedAccess>]
type Status =
    | [<CompiledName "queued">] Queued
    | [<CompiledName "pinning">] Pinning
    | [<CompiledName "pinned">] Pinned
    | [<CompiledName "failed">] Failed
    member this.Format() =
        match this with
        | Queued -> "queued"
        | Pinning -> "pinning"
        | Pinned -> "pinned"
        | Failed -> "failed"

///List of multiaddrs designated by pinning service for transferring any new data from external peers
type Delegates = list<string>
///Optional list of multiaddrs known to provide the data
type Origins = list<string>

///Alternative text matching strategy
[<Fable.Core.StringEnum; RequireQualifiedAccess>]
type TextMatchingStrategy =
    | [<CompiledName "exact">] Exact
    | [<CompiledName "iexact">] Iexact
    | [<CompiledName "partial">] Partial
    | [<CompiledName "ipartial">] Ipartial
    member this.Format() =
        match this with
        | Exact -> "exact"
        | Iexact -> "iexact"
        | Partial -> "partial"
        | Ipartial -> "ipartial"

///Response used for listing pin objects matching request
type PinResults =
    { ///The total number of pin objects that exist for passed query filters
      count: int
      ///An array of PinStatus results
      results: list<PinStatus> }
    ///Creates an instance of PinResults with all optional fields initialized to None. The required fields are parameters of this function
    static member Create (count: int, results: list<PinStatus>): PinResults = { count = count; results = results }

///Pin object with status
type PinStatus =
    { ///Globally unique identifier of the pin request; can be used to check the status of ongoing pinning, or pin removal
      requestid: string
      ///Status a pin object can have at a pinning service
      status: Status
      ///Immutable timestamp indicating when a pin request entered a pinning service; can be used for filtering results and pagination
      created: System.DateTimeOffset
      ///Pin object
      pin: Pin
      ///List of multiaddrs designated by pinning service for transferring any new data from external peers
      delegates: list<string>
      ///Optional info for PinStatus response
      info: Option<Map<string, string>> }
    ///Creates an instance of PinStatus with all optional fields initialized to None. The required fields are parameters of this function
    static member Create (requestid: string,
                          status: Status,
                          created: System.DateTimeOffset,
                          pin: Pin,
                          delegates: list<string>): PinStatus =
        { requestid = requestid
          status = status
          created = created
          pin = pin
          delegates = delegates
          info = None }

///Pin object
type Pin =
    { ///Content Identifier (CID) to be pinned recursively
      cid: string
      ///Optional name for pinned data; can be used for lookups later
      name: Option<string>
      ///Optional list of multiaddrs known to provide the data
      origins: Option<list<string>>
      ///Optional metadata for pin object
      meta: Option<Map<string, string>> }
    ///Creates an instance of Pin with all optional fields initialized to None. The required fields are parameters of this function
    static member Create (cid: string): Pin =
        { cid = cid
          name = None
          origins = None
          meta = None }

///Optional metadata for pin object
type PinMeta = Newtonsoft.Json.Linq.JToken
///Optional info for PinStatus response
type StatusInfo = Newtonsoft.Json.Linq.JToken

type Error =
    { ///Mandatory string identifying the type of error
      reason: string
      ///Optional, longer description of the error; may include UUID of transaction for support, links to documentation etc
      details: Option<string> }
    ///Creates an instance of Error with all optional fields initialized to None. The required fields are parameters of this function
    static member Create (reason: string): Error = { reason = reason; details = None }

///Response for a failed request
type Failure =
    { error: Error }
    ///Creates an instance of Failure with all optional fields initialized to None. The required fields are parameters of this function
    static member Create (error: Error): Failure = { error = error }

[<RequireQualifiedAccess>]
type GetPins =
    ///Successful response (PinResults object)
    | OK of payload: PinResults
    ///Error response (Bad request)
    | BadRequest of payload: Failure
    ///Error response (Unauthorized; access token is missing or invalid)
    | Unauthorized of payload: Failure
    ///Error response (The specified resource was not found)
    | NotFound of payload: Failure
    ///Error response (Insufficient funds)
    | Conflict of payload: Failure

[<RequireQualifiedAccess>]
type PostPins =
    ///Successful response (PinStatus object)
    | Accepted of payload: PinStatus
    ///Error response (Bad request)
    | BadRequest of payload: Failure
    ///Error response (Unauthorized; access token is missing or invalid)
    | Unauthorized of payload: Failure
    ///Error response (The specified resource was not found)
    | NotFound of payload: Failure
    ///Error response (Insufficient funds)
    | Conflict of payload: Failure

[<RequireQualifiedAccess>]
type GetPinsByRequestid =
    ///Successful response (PinStatus object)
    | OK of payload: PinStatus
    ///Error response (Bad request)
    | BadRequest of payload: Failure
    ///Error response (Unauthorized; access token is missing or invalid)
    | Unauthorized of payload: Failure
    ///Error response (The specified resource was not found)
    | NotFound of payload: Failure
    ///Error response (Insufficient funds)
    | Conflict of payload: Failure

[<RequireQualifiedAccess>]
type PostPinsByRequestid =
    ///Successful response (PinStatus object)
    | Accepted of payload: PinStatus
    ///Error response (Bad request)
    | BadRequest of payload: Failure
    ///Error response (Unauthorized; access token is missing or invalid)
    | Unauthorized of payload: Failure
    ///Error response (The specified resource was not found)
    | NotFound of payload: Failure
    ///Error response (Insufficient funds)
    | Conflict of payload: Failure

[<RequireQualifiedAccess>]
type DeletePinsByRequestid =
    ///Successful response (no body, pin removed)
    | Accepted
    ///Error response (Bad request)
    | BadRequest of payload: Failure
    ///Error response (Unauthorized; access token is missing or invalid)
    | Unauthorized of payload: Failure
    ///Error response (The specified resource was not found)
    | NotFound of payload: Failure
    ///Error response (Insufficient funds)
    | Conflict of payload: Failure
