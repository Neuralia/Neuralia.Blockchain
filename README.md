# Neuralia.Blockchain

The essential blockchain components of the Neuralia Blockchain.

### Neuralia.Blockchains.Core
The core components, tools and utilities used by the blockchain
### Neuralia.Blockchains.Common
The base Blockchain implementation

## Build Instructions

##### First, ensure dotnet core 2.2 SDK is installed

#### The first step is to ensure that the dependencies have been built and copied into the nuget-source folder.

 - Neuralia.Blockchains.Tools
 - Neuralia.System.Data.HashFunction.xxHash
 - Neuralia.STUN
 - Neuralia.BouncyCastle

Then, simply invoke pack.sh
> ./pack.sh
this will produce two packages named **Neuralia.Blockchains.Core.*[version]*.nupkg** and **Neuralia.Blockchains.Common.*[version]*.nupkg**
