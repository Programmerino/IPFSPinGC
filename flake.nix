{
  description = "IPFSPinGC";

  nixConfig.bash-prompt = "\[nix-develop\]$ ";

  inputs.nixpkgs.url = "github:nixos/nixpkgs";

  inputs.flake-utils.url = "github:numtide/flake-utils";

  inputs.dotnet.url = "github:Programmerino/dotnet-nix";

  inputs.bundler.url = "github:NixOS/bundlers";

  outputs = { self, nixpkgs, flake-utils, dotnet, bundler }:
    flake-utils.lib.eachSystem(["x86_64-linux" "aarch64-linux"]) (system:
      let
        pkgs = import nixpkgs { 
          inherit system;
        };
        name = "IPFSPinGC";
        version = let _ver = builtins.getEnv "GITVERSION_NUGETVERSIONV2"; in if _ver == "" then "0.0.0" else "${_ver}.${builtins.getEnv "GITVERSION_COMMITSSINCEVERSIONSOURCE"}";
        sdk = pkgs.dotnet-sdk;
        library = false;

        builder = library: dotnet.buildDotNetProject.${system} rec {
              inherit name;
              inherit version;
              inherit sdk;
              inherit system;
              inherit library;
              src = ./.;

              nativeBuildInputs = [
                pkgs.clang_12
              ];

              project = "IPFSPinGC.fsproj";
              lockFile = ./packages.lock.json;
              configFile =./nuget.config;
              nugetSha256 = "sha256-JtailvLQXYR+jAw8GM0EfqPVJzUMp1u2Wy1EctFnrPE=";
        };

      in rec {
          devShell = pkgs.mkShell {
            inherit name;
            inherit version;
            inherit library;
            DOTNET_CLI_HOME = "/tmp/dotnet_cli";
            DOTNET_CLI_TELEMTRY_OPTOUT=1;
            CLR_OPENSSL_VERSION_OVERRIDE=1.1;
            DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1;
            DONTET_ROOT = "${sdk}";
            buildInputs = defaultPackage.nativeBuildInputs ++ [ pkgs.starship sdk pkgs.git ];
            shellHook = ''
              eval "$(starship init bash)"
            '';
          };
    
          packages."${name}" = builder library;
          packages.nuget = builder true;

          defaultPackage = packages."${name}";

          packages.bundle-arx = bundler.bundlers."${system}".toArx defaultPackage;
          packages.bundle-rpm = bundler.bundlers."${system}".toRPM defaultPackage;
          packages.bundle-deb = bundler.bundlers."${system}".toDEB defaultPackage;
          packages.bundle-docker = bundler.bundlers."${system}".toDockerImage defaultPackage;

          packages.release = pkgs.stdenv.mkDerivation {
                              inherit version;
                              name = "${name}-release";
                              dontStrip = true;
                              dontPatch = true;
                              dontFetch = true;
                              dontUnpack = true;
                              dontBuild = true;
                              dontConfigure = true;
                              installPhase = ''
                                mkdir -p $out
                                cp -s ${packages.nuget}/*.nupkg $out
                                cp -s ${packages.bundle-arx} $out/${name}-${system}.arx
                                cp -s ${packages.bundle-rpm}/*.rpm $out/${name}-${system}.rpm
                                cp -s ${packages.bundle-deb}/*.deb $out/${name}-${system}.deb
                                cp -s ${packages.bundle-docker} $out/${name}-${system}-dockerimage.tar
                              '';
                            };

          apps."${system}" = {
            type = "app";
            program = "${defaultPackage}/bin/${name}";
          };

          defaultApp = apps."${system}";

      }
    );
}