open System
open Microsoft.Extensions.Configuration
open PGNIG
open Utils
open Energa
open Alior

// This is only a dummy app that I use to test the clients.
// Currently the clients are meant to be used in an FSI environment.
// Install them via paket's github file reference.

let testAlior () =
    let c = AliorClient(env "ALIOR_USERNAME", env "ALIOR_PASSWORD", isTest=true)

    let dummyTaxTransfer = Transfers.Row("",    "asdf", "84101000712221000000000000", "2024/April", 123M, DateTimeOffset.UtcNow, "asdf", "asdf")
    let dummyRegTransfer = Transfers.Row("uni", "asdf", "91113000070080239435200002", "asdf", 1M, DateTimeOffset.UtcNow, "asdf", "asdf")

    c.SignIn()
    c.Scrape(count=1) |> ignore
    c.GetP() |> ignore
    c.TransferTax(dummyTaxTransfer, "Pierwszy Urząd Skarbowy Warszawa")
    c.TransferRegular dummyRegTransfer

    printfn "if we reached this line without errors all must be good!"

let testPGNIG args =
    let c = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", args=args, isTest=true)
    c.SignIn()
    c.SubmitIndication 123

let testEnerga () =
    let c = EnergaClient(env "ENERGA_USERNAME", env "ENERGA_PASSWORD", isTest=true)
    c.SignIn()
    let d = c.SubmitIndication("a", 123)
    printfn "Submitted indication, received response: %A" d

[<EntryPoint>]
let main _ =
    let config =
        ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
            .Build()

    let args =
        config.GetSection("args").GetChildren()
            |> Seq.map (fun s -> s.Value)
            |> Seq.toArray

    testAlior ()

    testPGNIG args

    testEnerga ()

    0