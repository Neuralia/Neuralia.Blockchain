# Neuralia.Blockchain

##### Version:  MAINNET 1.0.1.5

The essential blockchain components of the Neuralia Blockchain.

### Neuralia.Blockchains.Core
The core components, tools and utilities used by the blockchain
### Neuralia.Blockchains.Components
Some basic building blocks for the blockchain constructs
### Neuralia.Blockchains.Common
The base Blockchain implementation

## Build Instructions

##### First, ensure dotnet core 5.0 SDK is installed

#### The first step is to ensure that the dependencies have been built and copied into the local-source folder.

 - Neuralia.Blockchains.Tools
 - Neuralia.BouncyCastle
 - Neuralia.NClap
 - Neuralia.Open.Nat

Then, simply invoke pack.sh
> ./pack.sh
this will produce two packages named **Neuralia.Blockchains.Core.*[version]*.nupkg**, **Neuralia.Blockchains.Components.*[version]*.nupkg** and **Neuralia.Blockchains.Common.*[version]*.nupkg**
