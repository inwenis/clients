#load "c:/git/prelude/prelude.fsx"

Prelude.WorkingDirectorySetter.SetToMe()

open System

// --- header --- //

#load "local_prelude.fsx"
#load "gpt_printer.fsx"

#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 20.2.2"

#load "../src/Utils.fs"
#load "../src/pgn.fs"

open System.IO
open System
open PuppeteerSharp

open Utils
open PGNIG

let args = [| "--disable-notifications"; "--force-device-scale-factor=0.9" |]


let c = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", args)
c.SignIn()
let mutable p = c.GetP()
let x = p.QuerySelectorAsync("xpath///div").Result


