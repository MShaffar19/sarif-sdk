#!/bin/bash
rm ./lib/Autogenerated/*
npm install
./node_modules/.bin/json2ts --input ../../Sarif/Schemata/Sarif.schema.json --output ./lib/Autogenerated/sarif-types.ts
./node_modules/.bin/tsc