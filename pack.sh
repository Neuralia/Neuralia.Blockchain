#!/bin/bash


dotnet restore

if dotnet build -c Release --no-incremental ; then
    if  dotnet pack -c Release -o ../../ ; then
	dotnet clean ;
         echo "pack completed"
    else
        echo "build failed"
    fi
else
    echo "build failed"
fi


   
