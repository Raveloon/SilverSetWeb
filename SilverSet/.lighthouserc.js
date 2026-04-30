name: Lighthouse CI
on: [push]
jobs:
lighthouse:
runs - on: ubuntu - latest
steps:
- uses: actions / checkout@v3

- name: Setup.NET
uses: actions / setup - dotnet@v3
with:
dotnet - version: '9.0'

    - name: Setup Node.js
uses: actions / setup - node@v3
with:
node - version: 18

    - name: Install LHCI
run: npm install - g @lhci/cli@0.12.x

    - name: Build Project
run: dotnet build

    - name: Run Lighthouse CI
run: lhci autorun
env:
LHCI_GITHUB_APP_TOKEN: ${ { secrets.LHCI_GITHUB_APP_TOKEN } }