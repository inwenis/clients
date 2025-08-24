open System
open Microsoft.Extensions.Configuration

[<EntryPoint>]
let main argv =
    // Build configuration
    let config =
        ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
            .Build()

    // Read values
    let greeting = config.["AppSettings:Greeting"]
    let retryCount = config.["AppSettings:RetryCount"] |> int

    // Use values
    printfn "%s" greeting
    printfn "RetryCount = %d" retryCount

    0