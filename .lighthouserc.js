module.exports = {
  ci: {
    collect: {
      startServerCommand: "dotnet run --project SilverSet/SilverSet.csproj --no-build --configuration Release --urls http://localhost:5000",
      url: ["http://localhost:5000/"],
      numberOfRuns: 1,
      startServerReadyPattern: "Now listening on",
      startServerReadyTimeout: 180000
    },
    upload: {
      target: "temporary-public-storage"
    }
  }
};