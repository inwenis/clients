open System
open Microsoft.Extensions.Configuration
open PGNIG
open Utils

[<EntryPoint>]
let main argv =
    let config =
        ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
            .Build()


    let args =
        config.GetSection("args").GetChildren()
            |> Seq.map (fun s -> s.Value)
            |> Seq.toArray

    printfn "%A" args

    let client = PGNiGClient(env "PGNIG_USERNAME", env "PGNIG_PASSWORD", args)

    client.SignIn()

    let p = client.GetP()

    printfn "%A" p

    printfn "%A" (p.QuerySelectorAsync("xpath///div").Result)

    0