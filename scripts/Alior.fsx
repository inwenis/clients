#load "c:/git/prelude/prelude.fsx"

Prelude.WorkingDirectorySetter.SetToMe()

open System

// --- header --- //

#r "nuget: FSharp.Data, 6.6"
#r "nuget: PuppeteerSharp, 20.2.2"

#load "../src/Utils.fs"
#load "../src/Alior.fs"

#load "gpt_printer.fsx"

open System.IO
open System
open PuppeteerSharp

open Utils
open Alior

let c = new AliorClient(env "ALIOR_USERNAME", env "ALIOR_PASSWORD", isTest=true)

// #load "../src/Alior.fs"
// open Alior
// let c = AliorClient(env "ALIOR_USERNAME", env "ALIOR_PASSWORD", page = p, isTest=true)

let taxOfficeName = "Drugi Mazowiecki Urząd Skarbowy Warszawa"
let transferTax = Transfers.Row("", "asdf", "84101000712221000000000000", "2024/April", 123M, DateTimeOffset.UtcNow,  "asdf", "asdf")
let transferReg = Transfers.Row("Batorego 104", "asdf", "91113000070080239435200002", "asdf", 1M, DateTimeOffset.UtcNow,  "asdf", "asdf")

c.SignIn()

c.TransferRegular(transferReg)

c.Scrape(period=All)
let p = c.GetP()
