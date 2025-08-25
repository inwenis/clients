#load "c:/git/prelude/prelude.fsx"

Prelude.WorkingDirectorySetter.SetToMe()

open System

// --- header --- //

#load "local_prelude.fsx"
#load "gpt_printer.fsx"

#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.3"

#load "../src/Utils.fs"
#load "../src/pgn.fs"

open System.IO
open System
open PuppeteerSharp

open clients
open clients.Utils
open clients.PGNIG

let args = [| "--disable-notifications"; "--force-device-scale-factor=0.9" |]

#load "../src/Utils.fs"
#load "../src/pgn.fs"
open clients.PGNIG

let c = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", args)

c.SignIn()
let mutable p = c.GetP()

let x = p.QuerySelectorAsync("xpath///div").Result

c.SubmitIndication(123)
