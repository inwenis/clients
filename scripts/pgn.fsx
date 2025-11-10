#load "c:/git/prelude/prelude.fsx"
Prelude.WorkingDirectorySetter.SetToMe()
// --- header --- //

#load "local_prelude.fsx"
#load "gpt_printer.fsx"

#r "nuget: FSharp.Data, 6.6"
#r "nuget: PuppeteerSharp, 20.2.2"

#load "../src/Utils.fs"
#load "../src/pgn.fs"

open System.IO
open System
open PuppeteerSharp

open Utils
open PGNIG

let c = new PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", isTest=true)
c.SignIn()
c.SubmitIndication 999999

// #load "../src/pgn.fs"
// open PGNIG
// let c = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", page=c.GetP(), isSignedIn=false)
// let c = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", page=c.GetP(), isSignedIn=true)

let p = c.GetP()

// clickOrContinue p "xpath///i[contains(@class,'icon-close')]"

p.Dispose()
