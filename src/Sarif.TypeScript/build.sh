#!/bin/bash
rm ./lib/Autogenerated/*
./node_modules/.bin/json2ts --input ../Sarif/Schemata/sarif-schema.json --output ./lib/Autogenerated/sarif-types.ts
tsc