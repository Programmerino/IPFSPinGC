# IPFSPinGC

A tool to garbage collect IPFS pinning service pins based on age or size

## Building

This project uses Nix (you will need to enable flakes) and you can accelerate builds by using the binary cache `programmerino` with the command:
```
cachix use programmerino
```

and ultimately build with `nix build` which will place a binary in `result`.

## Installation

If you have Nix installed, you can use `nix profile install github:Programmerino/IPFSPinGC` or `nix run github:Programmerino/IPFSPinGC` to try it without installation. Otherwise, you can try one of the GitHub releases or building it from source without Nix

## Usage

```
USAGE: IPFSPinGC [--help] [--pinata] [--accesstoken <token>] [--sortmethod <size|added>] [--gateway <URL>] [--dry] [--apiurl <URL>] <size>

SIZELIMIT:

    <size>                specify the size limit to garbage collect down to in megabytes

OPTIONS:

    --pinata, -p          use pinata to get the total pinned data size rather than counting all pins (more efficient if actually using pinata, also sets defaults)
    --accesstoken, -t <token>
                          specify the access token of your account (for pinata, see https://pinata.cloud/keys and use the JWT)
    --sortmethod <size|added>
                          specify the sort method to use (default is removing oldest pins). Size takes longer because it needs to query each CID to find its size
    --gateway, -g <URL>   specify the gateway to use for size checking (default is https://localhost:8080, if pinata is specified it is the pinata gateway)
    --dry, -d             dry run; do not actually delete any pins
    --apiurl, -a <URL>    specify the API url of the target pinning service (mandatory if pinata is not enabled)
    --help                display this list of options.
```

## Examples

### Pinata
This will use the Pinata-specific APIs to reduce pinned items to a size of 1GB provided a JWT key

```
IPFSPinGC 1000 -p -t JWT
```

### Other
```
IPFSPinGC 1000 -a APIURL --gateway ASSOCIATED_URL -t KEY
```