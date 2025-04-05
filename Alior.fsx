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

let ac = AliorClient(username, password, isTest=true)
// #load "Alior.fs"
//open clients.Alior
//let ac = AliorClient(username, password, ac.GetP(), true)
//ac.SignIn()

// let taxOfficeName = "Drugi Mazowiecki Urząd Skarbowy Warszawa"
// let transferTax = Transfers.Row("", "asdf", "84101000712221000000000000", "2024/April", 123M, DateTimeOffset.UtcNow,  "asdf", "asdf")
// let transferReg = Transfers.Row("", "asdf", "91113000070080239435200002", "asdf", 1M, DateTimeOffset.UtcNow,  "asdf", "asdf")

ac.Scrape(period=All, count=1)
