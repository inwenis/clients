# Clients

This repo contains clients I use to automate settling utilities.

# How to use it?

The clients are meant to be referenced via paket's [GitHub dependencies](https://fsprojects.github.io/Paket/github-dependencies.html) and used in an FSI (.fsx) environment.

# Sample usage

```pwsh
mkdir myFirstProj
cd myFirstProj
mkdir src
dotnet new console -lang f#
mv Program.fs src/Program.fs
mv myFirstProj.fsproj src/myFirstProj.fsproj
dotnet new tool-manifest
dotnet tool install paket
dotnet tool restore
dotnet paket init
dotnet paket add FSharp.Data    --project .\src\myFirstProj.fsproj
dotnet paket add PuppeteerSharp --project .\src\myFirstProj.fsproj

""                                           >> paket.dependencies
"github inwenis/clients src/Utils.fs"        >> paket.dependencies
"github inwenis/clients src/AliorParsing.fs" >> paket.dependencies

""                                           >> src/paket.references
"File:Utils.fs"                              >> src/paket.references
"File:AliorParsing.fs"                       >> src/paket.references

dotnet paket install
dotnet paket restore

"#r `"nuget: FSharp.Data, 6.4.0`""                                                                >> src/script.fsx
"#r `"nuget: PuppeteerSharp, 20.2.2`""                                                            >> src/script.fsx
""                                                                                                >> src/script.fsx
"#load `"../paket-files/inwenis/clients/src/Utils.fs`""                                           >> src/script.fsx
"#load `"../paket-files/inwenis/clients/src/AliorParsing.fs`""                                    >> src/script.fsx
""                                                                                                >> src/script.fsx
"open AliorParsing"                                                                               >> src/script.fsx
""                                                                                                >> src/script.fsx
"let rows = parseFiles [@`"C:\Users\inwen\Downloads\Historia_Operacji_2026-05-20_12-55-38.csv`"]" >> src/script.fsx

dotnet fsi src/script.fsx

```

# Snapshots

The clients occasionally save snapshots to `./snapshots/page_{timestamp}.mhtml`.
They should be ignored in your `.gitignore`.
They are meant for debugging if the client encounters an error, for e.x. when the page's has a different format than we expected.

# TODO

- consider better colorful logging - highlight xpath syntax
- add C# config tutorial
- add F# example
- add example using https://fsprojects.github.io/Paket/fsi-integration.html
- close clients when done, maybe implement IDisposable?
- make up your mind if you want to use wait for selector or query selector
- add error handling in helpers functions
- getP only returns something after you're signed in
- args are irrelevant in clients when page is given
- unify if clients automatically login in or you have to do it explicitly
- add a "keep being logged in background threat"
- think if dumping page in Alior clients poses a security risk
- make tests parallel and close windows after test
    - parallel tests will require synchronizing printing output
