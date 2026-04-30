module.exports = {
  ci: {
    collect: {
      startServerCommand: "dotnet run --project SilverSet/SilverSet.csproj --no-build --configuration Release --urls http://localhost:5295",
      url: [
        "http://localhost:5295/"
      ],
      numberOfRuns: 1,
      startServerReadyPattern: "Now listening on",
      startServerReadyTimeout: 180000,
      settings: {
        chromeFlags: "--no-sandbox --disable-setuid-sandbox --headless --ignore-certificate-errors"
      }
    },
    upload: {
      target: "temporary-public-storage"
    }
  }
};