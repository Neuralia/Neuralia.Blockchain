# Neuralia.Blockchain

##### Version:  Release Candidate 1

The essential blockchain components of the Neuralia Blockchain.

### Neuralia.Blockchains.Core
The core components, tools and utilities used by the blockchain
### Neuralia.Blockchains.Common
The base Blockchain implementation

## Build Instructions

##### First, ensure dotnet core 3.1 SDK is installed

#### The first step is to ensure that the dependencies have been built and copied into the local-source folder.

 - Neuralia.Data.HashFunction.xxHash
 - Neuralia.STUN
 - Neuralia.Blockchains.Tools
 - Neuralia.BouncyCastle

Then, simply invoke pack.sh
> ./pack.sh
this will produce two packages named **Neuralia.Blockchains.Core.*[version]*.nupkg** and **Neuralia.Blockchains.Common.*[version]*.nupkg**
