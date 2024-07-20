#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.3"

#load "Utils.fs"
#load "Alior.fs"

open System.IO
open System
open PuppeteerSharp

open clients
open clients.Utils
open clients.Alior

let username () = System.Environment.GetEnvironmentVariable("ALIOR_USERNAME")
let password () = System.Environment.GetEnvironmentVariable("ALIOR_PASSWORD")

let ac = AliorClient(username, password, true)
// let ac = AliorClient(username, password, p, true)
ac.SignIn()
let p = ac.GetP()

let this = ac

let taxOfficeName = "Drugi Mazowiecki Urząd Skarbowy Warszawa"
let transfer = Transfers.Row("", "asdf", "84101000712221000000000000", "2024/April", 123M, DateTimeOffset.UtcNow,  "asdf", "asdf")

#load "Alior.fs"
open clients.Alior
// let ac = AliorClient(username, password, p, true)
ac.TransferTax(transfer, taxOfficeName)
