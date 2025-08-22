#load "c:/git/prelude/prelude.fsx"
#load "local_prelude.fsx"

Prelude.WorkingDirectorySetter.SetToMe()

open System

// --- header --- //

#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.3"

#load "Utils.fs"
#load "pgn.fs"

open System.IO
open System
open PuppeteerSharp

open clients
open clients.Utils
open clients.PGNIG

#load "pgn.fs"
open clients.PGNIG

let c = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD")

c.SingIn()
let mutable p = c.GetP()
