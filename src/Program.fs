open System
open Microsoft.Extensions.Configuration
open PGNIG
open Utils
open Energa

// This is only a dummy app that I use to test the clients.
// Currently the clients are meant to be used in an FSI environment.
// Clients are meant to be referenced via paket's github file reference.

[<EntryPoint>]
let main argv =
    let config =
        ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
            .Build()

    let args =
        config.GetSection("args").GetChildren()
            |> Seq.map (fun s -> s.Value)
            |> Seq.toArray

    let client = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", args=args, isTest=true)
    client.SignIn()
    client.SubmitIndication 123

    let client = EnergaClient(env "ENERGA_USERNAME", env "ENERGA_PASSWORD", isTest=true)
    client.SignIn()
    let d = client.SubmitIndication("a", 123)
    printfn "Submitted indication, received response: %A" d

    0