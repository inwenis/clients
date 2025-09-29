open System
open PGNIG
open Utils
open Energa
open Alior

// This is only a dummy app that I use to test the clients.
// Currently the clients are meant to be used in an FSI environment.
// Install them via paket's github file reference.

let testAlior () =
    use c = new AliorClient(env "ALIOR_USERNAME", env "ALIOR_PASSWORD", isTest=true)
    let dummyTaxTransfer = Transfers.Row("",    "asdf", "84101000712221000000000000", "2024/April", 123M, DateTimeOffset.UtcNow, "asdf", "asdf")
    let dummyRegTransfer = Transfers.Row("uni", "asdf", "91113000070080239435200002", "asdf", 1M, DateTimeOffset.UtcNow, "asdf", "asdf")
    c.SignIn()
    c.Scrape(count=1) |> ignore
    c.GetP() |> ignore
    c.TransferTax(dummyTaxTransfer, "Pierwszy Urząd Skarbowy Warszawa")
    c.TransferRegular dummyRegTransfer
    printfn "if we reached this line without errors all must be good!"

let testPGNIG () =
    use c = new PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", isTest=true)
    c.SignIn()
    c.SubmitIndication 123
    c.ScrapeInvoices() |> ignore
    c.ScrapeOverpayments() |> ignore
    printfn "if we reached this line without errors all must be good!"

let testEnerga () =
    use c = new EnergaClient(env "ENERGA_USERNAME", env "ENERGA_PASSWORD", isTest=true)
    c.SignIn()
    c.SubmitIndication("a", 123) |> ignore
    printfn "if we reached this line without errors all must be good!"


[<EntryPoint>]
let main _ =
    testAlior ()

    testPGNIG ()

    testEnerga ()

    0