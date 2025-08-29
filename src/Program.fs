open System
open Microsoft.Extensions.Configuration
open PGNIG
open Utils
open Energa

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

    let client = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", args)
    client.SignIn()

    let client = EnergaClient(env "ENERGA_USERNAME", env "ENERGA_PASSWORD", isTest=true)
    client.SignIn()
    let d = client.SubmitIndication("a", 123)

    0