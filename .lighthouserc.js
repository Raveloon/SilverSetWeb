module.exports = {
    ci: {
        collect: {
            // dotnet run komutu projenin ana dizinindeki csproj dosyasını hedeflemeli
            startServerCommand: "dotnet run --project SilverSet/SilverSet.csproj --urls http://localhost:5295",
            url: [
                "http://localhost:5295/",
                "http://localhost:5295/Home/Index"
            ],
            numberOfRuns: 2,
            startServerReadyTimeout: 60000 
        },
        upload: {
            target: "temporary-public-storage"
        }
    }
};
