open System
open Microsoft.Extensions.Configuration
open PGNIG
open Utils
open Energa
open Alior

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

    let c1 = AliorClient(env "ALIOR_USERNAME", env "ALIOR_PASSWORD", isTest=true)
    c1.SignIn()
    let scraped = c1.Scrape(count=1)
    printfn "Scraped data: %A" scraped

    let c2 = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", args=args, isTest=true)
    c2.SignIn()
    c2.SubmitIndication 123

    let c3 = EnergaClient(env "ENERGA_USERNAME", env "ENERGA_PASSWORD", isTest=true)
    c3.SignIn()
    let d = c3.SubmitIndication("a", 123)
    printfn "Submitted indication, received response: %A" d

    0