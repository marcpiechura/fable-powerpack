// include Fake libs
#r "./packages/build/FAKE/tools/FakeLib.dll"
#r "System.IO.Compression.FileSystem"
#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
#load "paket-files/build/fable-compiler/fake-helpers/Fable.FakeHelpers.fs"

open Fake
open Fable.FakeHelpers
open Octokit

#if MONO
// prevent incorrect output encoding (e.g. https://github.com/fsharp/FAKE/issues/1196)
System.Console.OutputEncoding <- System.Text.Encoding.UTF8
#endif

let project = "fable-powerpack"
let gitOwner = "fable-compiler"

let dotnetcliVersion = "2.1.103"
let mutable dotnetExePath = environVarOrDefault "DOTNET" "dotnet"

let CWD = __SOURCE_DIRECTORY__

module Yarn =
    open YarnHelper

    let install workingDir =
        Yarn (fun p ->
            { p with
                Command = YarnCommand.Install InstallArgs.Standard
                WorkingDirectory = workingDir
            })

    let run workingDir script args =
        Yarn (fun p ->
            { p with
                Command = YarnCommand.Custom ("run " + script + " " + args)
                WorkingDirectory = workingDir
            })

// Clean and install dotnet SDK
Target "Bootstrap" (fun () ->
    !! "bin" ++ "obj" ++ "tests/bin" ++ "tests/obj" |> CleanDirs
    dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
)

Target "Test" (fun () ->
    Yarn.install CWD
    Yarn.run CWD "test" ""
)

Target "PublishPackages" (fun () ->
    [ "Fable.PowerPack.fsproj"]
    |> publishPackages CWD dotnetExePath
)

Target "GitHubRelease" (fun () ->
    let releasePath = CWD </> "RELEASE_NOTES.md"
    githubRelease releasePath gitOwner project (fun user pw release ->
        createClient user pw
        |> createDraft gitOwner project release.NugetVersion
            (release.SemVer.PreRelease <> None) release.Notes
        |> releaseDraft
        |> Async.RunSynchronously
    )
)

"Bootstrap"
==> "Test"
==> "PublishPackages"
==> "GitHubRelease"

RunTargetOrDefault "Bootstrap"
