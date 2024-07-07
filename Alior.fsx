#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.3"

#load "Utils.fs"
#load "Alior.fs"

open System.IO
open System
open PuppeteerSharp

open clients.Utils
open clients.Alior


let username () = System.Environment.GetEnvironmentVariable("ALIOR_USERNAME")
let password () = System.Environment.GetEnvironmentVariable("ALIOR_PASSWORD")

let ac = AliorClient(username, password)
ac.SignIn()

let p = ac.GetP()

// todo - fix paths in scraping
// todo make default timeout in waitForSelectorAndClick smaller
