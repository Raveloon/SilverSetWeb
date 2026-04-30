module.exports = {
  ci: {
    collect: {
      startServerCommand: "dotnet run --project SilverSet/SilverSet.csproj --no-build --configuration Release --urls http://localhost:5295",
      url: [
        "http://localhost:5295/",
        "http://localhost:5295/Home/Index"
      ],
      numberOfRuns: 1,
      startServerReadyPattern: "Now listening on",
      startServerReadyTimeout: 180000
    },
    upload: {
      target: "temporary-public-storage"
    }
  }
};