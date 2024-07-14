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

let ac = AliorClient(username, password)
ac.SignIn()
let p = ac.GetP()

let files = ac.Scrape()
files
|> List.map (fun x -> x.FullName)
// let ac2 = AliorClient(username, password, p)
// ac2.Scrape()
let this = ac
// todo make default timeout in waitForSelectorAndClick smaller

let taxOfficeName = "Kujawsko-Pomorski Urząd Skarbowy w Bydgoszczy"
let transfer = Transfers.Row("Kuni", "asdf", "asdf", "asdf", 123M, DateTimeOffset.UtcNow,  "asdf", "asdf")
