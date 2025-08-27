#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 20.2.2"

#load "../src/Utils.fs"
#load "../src/Alior.fs"

open System.IO
open System
open PuppeteerSharp

open clients
open clients.Utils
open clients.Alior

let ac = AliorClient(env "ALIOR_USERNAME", env "ALIOR_PASSWORD", isTest=true)
//#load "Alior.fs"
//open clients.Alior
//let ac = AliorClient(env "ALIOR_USERNAME", env "ALIOR_PASSWORD", ac.GetP(), isTest=true)
//ac.SignIn()

// let taxOfficeName = "Drugi Mazowiecki Urząd Skarbowy Warszawa"
// let transferTax = Transfers.Row("", "asdf", "84101000712221000000000000", "2024/April", 123M, DateTimeOffset.UtcNow,  "asdf", "asdf")
// let transferReg = Transfers.Row("", "asdf", "91113000070080239435200002", "asdf", 1M, DateTimeOffset.UtcNow,  "asdf", "asdf")

ac.Scrape(period=All)
