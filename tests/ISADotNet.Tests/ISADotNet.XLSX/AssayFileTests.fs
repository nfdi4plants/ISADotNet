﻿module AssayFileTests


open FSharpSpreadsheetML
open ISADotNet

open Expecto
open TestingUtils

open ISADotNet
open ISADotNet.XLSX
open ISADotNet.XLSX.AssayFile

[<Tests>]
let testColumnHeaderFunctions = 

    testList "ColumnHeaderFunctionTests" [
        testCase "RawKindTest" (fun () ->

            let headerString = "RawHeader"

            let header = AnnotationColumn.ColumnHeader.fromStringHeader headerString

            let testHeader = AnnotationColumn.ColumnHeader.create headerString "RawHeader" None None

            Expect.equal header testHeader "Should have used header string as kind"
        )
        testCase "NumberedRawKindTest" (fun () ->

            let headerString = "RawHeader (#5)"

            let header = AnnotationColumn.ColumnHeader.fromStringHeader headerString

            let testHeader = AnnotationColumn.ColumnHeader.create headerString "RawHeader" None (Some 5)

            Expect.equal header testHeader "Number was not parsed correctly"
        )
        testCase "NameWithNoOntology" (fun () ->

            let headerString = "NamedHeader [Name]"

            let header = AnnotationColumn.ColumnHeader.fromStringHeader headerString

            let testHeader = AnnotationColumn.ColumnHeader.create headerString "NamedHeader" (OntologyAnnotation.fromString "Name" "" "" |> Some) None

            Expect.equal header testHeader "Dit not parse Name correctly"
        )
        testCase "NumberedWithOntology" (fun () ->

            let headerString = "NamedHeader [Term] (#3; #tSource:Accession)"

            let header = AnnotationColumn.ColumnHeader.fromStringHeader headerString

            let testComment = Comment.fromString "Number" "3"
            let testOntology = OntologyAnnotation.create None (Some (AnnotationValue.Text "Term")) (Some (URI.fromString "Accession")) (Some "Source") (Some [testComment])
            let testHeader = AnnotationColumn.ColumnHeader.create headerString "NamedHeader" (Some testOntology) (Some 3)

            Expect.equal header testHeader "Dit not parse Name correctly"
        )
    ]

[<Tests>]
let testHeaderSplittingFunctions = 
    testList "HeaderSplittingFunctionTests" [
        testCase "SplitBySamplesOnlySource" (fun () ->

            let headers = ["Source Name";"Paramer [MyDude]";"Characteristic [MyGuy]"]

            let resultHeaders = AnnotationTable.splitBySamples headers

            Expect.hasLength resultHeaders 1 "Shouldn't have split headers, as only one source column was set"
            Expect.sequenceEqual headers (resultHeaders |> Seq.head) "Shouldn't have split headers, as only one source column was set"
        )
        testCase "SplitBySamplesOnlySample" (fun () ->

            let headers = ["Sample Name";"Paramer [MyDude]";"Characteristic [MyGuy]"]

            let resultHeaders = AnnotationTable.splitBySamples headers

            Expect.hasLength resultHeaders 1 "Shouldn't have split headers, as only one sample column was set"
            Expect.sequenceEqual (resultHeaders |> Seq.head) ["Paramer [MyDude]";"Characteristic [MyGuy]";"Sample Name"] "Should have moved sample to the end"
        )
        testCase "SplitBySamplesOnlySourceAndSample" (fun () ->

            let headers = ["Source Name";"Paramer [MyDude]";"Sample Name";"Characteristic [MyGuy]"]

            let resultHeaders = AnnotationTable.splitBySamples headers

            Expect.hasLength resultHeaders 1 "Shouldn't have split headers, as only one source column and sample column was set"
            Expect.sequenceEqual (resultHeaders |> Seq.head) ["Source Name";"Paramer [MyDude]";"Characteristic [MyGuy]";"Sample Name"] "Should have put source at front and sample at end"
        )
        testCase "SplitBySamplesManySamples" (fun () ->

            let headers = ["Source Name";"Paramer [MyDude]";"Sample Name";"Characteristic [MyGuy]";"Sample Name (#2)"]

            let resultHeaders = AnnotationTable.splitBySamples headers

            let testResults = seq [["Source Name";"Paramer [MyDude]";"Sample Name"];["Sample Name";"Characteristic [MyGuy]";"Sample Name (#2)"]] |> Seq.map (List.toSeq)

            Expect.hasLength resultHeaders 2 "Should have split headers, as several sample columns were set"
            Expect.sequenceEqual resultHeaders testResults "Headers were not split correctly"
        )
        testCase "splitByNamedProtocolsNoProtocols" (fun () ->

            let headers = ["Source Name";"Paramer [MyDude]";"Parameter [MyBro]";"Parameter [MyGuy]";"Sample Name"]

            let namedProtocols = []

            let resultHeaders = AnnotationTable.splitByNamedProtocols namedProtocols headers

            Expect.hasLength resultHeaders 1 "Shouldn't have split headers, as no named protocol was given"
            Expect.sequenceEqual headers (resultHeaders |> Seq.head |> snd) "Shouldn't have split headers, as no named protocol was given"
        )
        testCase "splitByNamedProtocols" (fun () ->

            let headers = ["Source Name";"Paramer [MyDude]";"Parameter [MyBro]";"Parameter [MyGuy]";"Sample Name"]

            let protocolWithName = Protocol.create None (Some "NamedProtocol") None None None None None None None
            let namedProtocols = [protocolWithName,seq ["Parameter [MyBro]";"Parameter [MyGuy]"]]

            let resultHeaders = AnnotationTable.splitByNamedProtocols namedProtocols headers |> Seq.map (fun (p,s) -> p, List.ofSeq s)

            let testResults = 
                [
                    Protocol.empty,["Source Name";"Paramer [MyDude]";"Sample Name"]
                    protocolWithName,["Source Name";"Parameter [MyBro]";"Parameter [MyGuy]";"Sample Name"]
                ]

            Expect.hasLength resultHeaders 2 "Should have split headers with the given protocol"
            Expect.isTrue (testResults.Head |> snd |> Seq.contains "Source Name" && testResults.Head |> snd |> Seq.contains "Sample Name") "Source and Sample column were not added to named Protocol"
            Expect.isTrue (testResults |> Seq.item 1|> snd |> Seq.contains "Source Name" && testResults |> Seq.item 1 |> snd |> Seq.contains "Sample Name") "Source and Sample column were not added to unnamed Protocol"
            Expect.sequenceEqual resultHeaders testResults "Headers were not split correctly"
        )
        testCase "splitByNamedProtocolsWrongHeaders" (fun () ->

            let headers = ["Source Name";"Paramer [MyDude]";"Parameter [MyBro]";"Parameter [MyGuy]";"Sample Name"]

            let protocolWithName = Protocol.create None (Some "NamedProtocol") None None None None None None None
            let namedProtocols = [protocolWithName,seq ["Parameter [Wreeeng]";"Parameter [Wraaang]"]]

            let resultHeaders = AnnotationTable.splitByNamedProtocols namedProtocols headers

            Expect.hasLength resultHeaders 1 "Shouldn't have split headers, as headers did not match"
            Expect.sequenceEqual headers (resultHeaders |> Seq.head |> snd) "Shouldn't have split headers, as headers did not match"
        )
    ]

[<Tests>]
let testNodeGetterFunctions =

    let sourceDirectory = __SOURCE_DIRECTORY__ + @"/TestFiles/"

    let assayFilePath = System.IO.Path.Combine(sourceDirectory,"AssayTableTestFile.xlsx")
    
    let doc = Spreadsheet.fromFile assayFilePath false
    let sst = Spreadsheet.tryGetSharedStringTable doc
    let wsp = Spreadsheet.tryGetWorksheetPartBySheetIndex 0u doc |> Option.get
    let table = Table.tryGetByNameBy (fun s -> s.Contains "annotationTable") wsp |> Option.get

    let m = Table.toSparseValueMatrix sst (Worksheet.getSheetData wsp.Worksheet) table
        
    testList "NodeGetterTests" [
        testCase "RetreiveValueFromMatrix" (fun () ->
            let tryGetValue k (dict:System.Collections.Generic.Dictionary<'K,'V>) = 
                let b,v = dict.TryGetValue(k)
                if b then Some v
                else None

            let v = tryGetValue ("Unit [square centimeter] (#h; #tUO:0000081; #u)",0) m

            Expect.isSome v "Value could not be retrieved from matrix"

            let expectedValue = "square centimeter"

            Expect.equal v.Value expectedValue "Value retrieved from matrix is not correct"

        )

        testCase "GetUnitGetter" (fun () ->

            let headers = ["Unit [square centimeter] (#h; #tUO:0000081; #u)";"Term Source REF [square centimeter] (#h; #tUO:0000081; #u)";"Term Accession Number [square centimeter] (#h; #tUO:0000081; #u)"]

            let unitGetterOption = AnnotationNode.tryGetUnitGetterFunction headers

            Expect.isSome unitGetterOption "Unit Getter was not returned even though headers should have matched"

            let unitGetter = unitGetterOption.Value

            let unit = unitGetter m 0

            let expectedUnit = OntologyAnnotation.fromString "square centimeter" "http://purl.obolibrary.org/obo/UO_0000081" "UO" 

            Expect.equal unit expectedUnit "Retrieved Unit is wrong"
        )
        testCase "GetUnitGetterWrongHeaders" (fun () ->

            let headers = ["Parameter [square centimeter] (#h; #tUO:0000081; #u)";"Term Source REF [square centimeter] (#h; #tUO:0000081; #u)";"Term Accession Number [square centimeter] (#h; #tUO:0000081; #u)"]

            let unitGetterOption = AnnotationNode.tryGetUnitGetterFunction headers

            Expect.isNone unitGetterOption "Unit Getter was returned even though headers should not have matched"
        )
        testCase "GetCharacteristicsGetter" (fun () ->

            let headers = ["Characteristics [leaf size]";"Term Source REF [leaf size] (#h; #tTO:0002637)";"Term Accession Number [leaf size] (#h; #tTO:0002637)";"Unit [square centimeter] (#h; #tUO:0000081; #u)";"Term Source REF [square centimeter] (#h; #tUO:0000081; #u)";"Term Accession Number [square centimeter] (#h; #tUO:0000081; #u)"]

            let characteristicGetterOption = AnnotationNode.tryGetCharacteristicGetter headers

            Expect.isSome characteristicGetterOption "Characteristic Getter was not returned even though headers should have matched"

            let characteristic,characteristicValueGetter = characteristicGetterOption.Value

            Expect.isSome characteristic "CharacteristGetter was returned but not characteristic was returned"

            let expectedCharacteristic = MaterialAttribute.fromString "leaf size" "0002637" "TO" 

            Expect.equal characteristic.Value expectedCharacteristic "Retrieved Characteristic is wrong"

            let expectedUnit = OntologyAnnotation.fromString "square centimeter" "http://purl.obolibrary.org/obo/UO_0000081" "UO" |> Some

            let expectedValue = Value.fromOptions (Some "10") None None

            let expectedCharacteristicValue = MaterialAttributeValue.create None (Some expectedCharacteristic) expectedValue expectedUnit

            let characteristicValue = characteristicValueGetter m 0

            Expect.equal characteristicValue expectedCharacteristicValue "Retrieved Characteristic Value was not correct"
        )
        testCase "GetCharacteristicsGetterWrongHeaders" (fun () ->

            let headers = ["Parameter [square centimeter] (#h; #tUO:0000081; #u)";"Term Source REF [square centimeter] (#h; #tUO:0000081; #u)";"Term Accession Number [square centimeter] (#h; #tUO:0000081; #u)"]

            let unitGetterOption = AnnotationNode.tryGetCharacteristicGetter headers

            Expect.isNone unitGetterOption "Characteristic Getter was returned even though headers should not have matched"
        )
        testCase "GetFactorGetter" (fun () ->

            let headers = ["Factor [time]";"Term Source REF [time] (#h; #tPATO:0000165)";"Term Accession Number [time] (#h; #tPATO:0000165)";"Unit [hour] (#h; #tUO:0000032; #u)";"Term Source REF [hour] (#h; #tUO:0000032; #u)";"Term Accession Number [hour] (#h; #tUO:0000032; #u)"]

            let factorGetterOption = AnnotationNode.tryGetFactorGetter headers

            Expect.isSome factorGetterOption "Factor Getter was not returned even though headers should have matched"
            
            let factor,factorValueGetter = factorGetterOption.Value

            Expect.isSome factor "FactorGetter was returned but no factor was returned"

            let expectedFactor = Factor.fromString "time" "time" "0000165" "PATO" 

            Expect.equal factor.Value expectedFactor "Retrieved Factor is wrong"
            
            let expectedUnit = OntologyAnnotation.fromString "hour" "http://purl.obolibrary.org/obo/UO_0000032" "UO" |> Some

            let expectedValue = Value.fromOptions (Some "5") None None

            let expectedFactorValue = FactorValue.create None (Some expectedFactor) expectedValue expectedUnit

            let factorValue = factorValueGetter m 0

            Expect.equal factorValue expectedFactorValue "Retrieved Factor Value was not correct"
        )
        testCase "GetFactorGetterWrongHeaders" (fun () ->

            let headers = ["Parameter [square centimeter] (#h; #tUO:0000081; #u)";"Term Source REF [square centimeter] (#h; #tUO:0000081; #u)";"Term Accession Number [square centimeter] (#h; #tUO:0000081; #u)"]

            let unitGetterOption = AnnotationNode.tryGetFactorGetter headers

            Expect.isNone unitGetterOption "Facotr Getter was returned even though headers should not have matched"
        )
        testCase "GetParameterGetter" (fun () ->

            let headers = ["Parameter [temperature unit]";"Term Source REF [temperature unit] (#h; #tUO:0000005)";"Term Accession Number [temperature unit] (#h; #tUO:0000005)";"Unit [degree Celsius] (#h; #tUO:0000027; #u)";"Term Source REF [degree Celsius] (#h; #tUO:0000027; #u)";"Term Accession Number [degree Celsius] (#h; #tUO:0000027; #u)"]

            let parameterGetterOption = AnnotationNode.tryGetParameterGetter headers

            Expect.isSome parameterGetterOption "Parameter Getter was not returned even though headers should have matched"
            
            let parameter,parameterValueGetter = parameterGetterOption.Value

            Expect.isSome parameter "ParameterGetter was returned but no parameter was returned"

            let expectedParameter = ProtocolParameter.fromString "temperature unit" "0000005" "UO" 

            Expect.equal parameter.Value expectedParameter "Retrieved Parameter is wrong"

            let expectedUnit = OntologyAnnotation.fromString "degree Celsius" "http://purl.obolibrary.org/obo/UO_0000027" "UO" |> Some

            let expectedValue = Value.fromOptions (Some "27") None None

            let expectedParameterValue = ProcessParameterValue.create (Some expectedParameter) expectedValue expectedUnit

            let parameterValue = parameterValueGetter m 0

            Expect.equal parameterValue expectedParameterValue "Retrieved Parameter Value was not correct"
        )
        testCase "GetParameterGetterNoUnit" (fun () ->

            let headers = ["Parameter [measurement device]";"Term Source REF [measurement device] (#h; #tOBI:0000832)";"Term Accession Number [measurement device] (#h; #tOBI:0000832)"]

            let parameterGetterOption = AnnotationNode.tryGetParameterGetter headers

            Expect.isSome parameterGetterOption "Parameter Getter was not returned even though headers should have matched"
            
            let parameter,parameterValueGetter = parameterGetterOption.Value

            Expect.isSome parameter "ParameterGetter was returned but no parameter was returned"

            Expect.isSome parameter.Value.ParameterName.Value.TermSourceREF "dawdawdawd"

            let expectedParameter = ProtocolParameter.fromString "measurement device" "0000832" "OBI" 

            Expect.equal parameter.Value expectedParameter "Retrieved Parameter is wrong"
            
            let expectedValue = Value.fromOptions (Some "Bruker NMR probe") (Some "http://purl.obolibrary.org/obo/OBI_0000561") (Some "OBI")

            let expectedParameterValue = ProcessParameterValue.create (Some expectedParameter) expectedValue None

            let parameterValue = parameterValueGetter m 0

            Expect.equal parameterValue expectedParameterValue "Retrieved Unitless Parameter Value was not correct"
        )
        testCase "GetParameterGetterUserSpecific" (fun () ->

            let headers = ["Parameter [heating block]";"Term Source REF [heating block] (#h; #tOBI:0400108)";"Term Accession Number [heating block] (#h; #tOBI:0400108)"]

            let parameterGetterOption = AnnotationNode.tryGetParameterGetter headers

            Expect.isSome parameterGetterOption "Parameter Getter was not returned even though headers should have matched"
            
            let parameter,parameterValueGetter = parameterGetterOption.Value

            Expect.isSome parameter "ParameterGetter was returned but no parameter was returned"

            let expectedParameter = ProtocolParameter.fromString "heating block" "0400108" "OBI" 

            Expect.equal parameter.Value expectedParameter "Retrieved Parameter is wrong"
            
            let expectedValue = Value.fromOptions (Some "Freds old stove") None None

            let expectedParameterValue = ProcessParameterValue.create (Some expectedParameter) expectedValue None

            let parameterValue = parameterValueGetter m 0

            Expect.equal parameterValue expectedParameterValue "Retrieved Unitless Parameter Value was not correct"
        )
        testCase "GetParameterGetterWrongHeaders" (fun () ->

            let headers = ["Factor [square centimeter] (#h; #tUO:0000081; #u)";"Term Source REF [square centimeter] (#h; #tUO:0000081; #u)";"Term Accession Number [square centimeter] (#h; #tUO:0000081; #u)"]

            let unitGetterOption = AnnotationNode.tryGetParameterGetter headers

            Expect.isNone unitGetterOption "Facotr Getter was returned even though headers should not have matched"
        )
    ]

[<Tests>]
let testProcessGetter =

    let sourceDirectory = __SOURCE_DIRECTORY__ + @"/TestFiles/"

    let assayFilePath = System.IO.Path.Combine(sourceDirectory,"AssayTableTestFile.xlsx")
    
    let doc = Spreadsheet.fromFile assayFilePath false
    let sst = Spreadsheet.tryGetSharedStringTable doc
    let wsp = Spreadsheet.tryGetWorksheetPartBySheetIndex 0u doc |> Option.get
    let table = Table.tryGetByNameBy (fun s -> s.Contains "annotationTable") wsp |> Option.get

    let m = Table.toSparseValueMatrix sst (Worksheet.getSheetData wsp.Worksheet) table

    let characteristicHeaders = ["Characteristics [leaf size]";"Term Source REF [leaf size] (#h; #tTO:0002637)";"Term Accession Number [leaf size] (#h; #tTO:0002637)";"Unit [square centimeter] (#h; #tUO:0000081; #u)";"Term Source REF [square centimeter] (#h; #tUO:0000081; #u)";"Term Accession Number [square centimeter] (#h; #tUO:0000081; #u)"]
    let expectedCharacteristic = MaterialAttribute.fromString "leaf size" "0002637" "TO" 
    let expectedCharacteristicUnit = OntologyAnnotation.fromString "square centimeter" "http://purl.obolibrary.org/obo/UO_0000081" "UO" |> Some
    let expectedCharacteristicValue = MaterialAttributeValue.create None (Some expectedCharacteristic) (Value.fromOptions (Some "10") None None) expectedCharacteristicUnit

    let factorHeaders = ["Factor [time]";"Term Source REF [time] (#h; #tPATO:0000165)";"Term Accession Number [time] (#h; #tPATO:0000165)";"Unit [hour] (#h; #tUO:0000032; #u)";"Term Source REF [hour] (#h; #tUO:0000032; #u)";"Term Accession Number [hour] (#h; #tUO:0000032; #u)"]
    let expectedFactor = Factor.fromString "time" "time" "0000165" "PATO" 
    let expectedFactorUnit = OntologyAnnotation.fromString "hour" "http://purl.obolibrary.org/obo/UO_0000032" "UO" |> Some
    let expectedFactorValue = FactorValue.create None (Some expectedFactor) (Value.fromOptions (Some "5") None None) expectedFactorUnit

    let parameterHeaders = ["Parameter [temperature unit]";"Term Source REF [temperature unit] (#h; #tUO:0000005)";"Term Accession Number [temperature unit] (#h; #tUO:0000005)";"Unit [degree Celsius] (#h; #tUO:0000027; #u)";"Term Source REF [degree Celsius] (#h; #tUO:0000027; #u)";"Term Accession Number [degree Celsius] (#h; #tUO:0000027; #u)"]
    let expectedParameter = ProtocolParameter.fromString "temperature unit" "0000005" "UO" 
    let expectedParameterUnit = OntologyAnnotation.fromString "degree Celsius" "http://purl.obolibrary.org/obo/UO_0000027" "UO" |> Some
    let expectedParameterValue = ProcessParameterValue.create (Some expectedParameter) (Value.fromOptions (Some "27") None None) expectedParameterUnit

    let sourceHeader = ["Source Name"]
    let expectedSourceName = "Source1"

    let sampleHeader = ["Sample Name"]
    let expectedSampleName = "Sample1"

    testList "ProcessGetterTests" [
        testCase "ProcessGetterSourceParamsSample" (fun () ->

            let headers = sourceHeader @ characteristicHeaders @ factorHeaders @ parameterHeaders @ sampleHeader

            let expectedSource = Source.create None (Some expectedSourceName) (Some [expectedCharacteristicValue])
            let expectedInput = ProcessInput.Source expectedSource
            let expectedOutput = ProcessOutput.Sample (Sample.create None (Some expectedSampleName) (Some [expectedCharacteristicValue]) (Some [expectedFactorValue]) (Some [expectedSource])  )

            let expectedProtocol = Protocol.create None None None None None None (Some [expectedParameter]) None None

            let expectedProcess = Process.create None None (Some expectedProtocol) (Some [expectedParameterValue]) None None None None (Some [expectedInput]) (Some [expectedOutput]) None

            let characteristics,factors,protocol,processGetter = AnnotationTable.getProcessGetter Protocol.empty (headers |> AnnotationNode.splitIntoNodes)

            Expect.sequenceEqual characteristics [expectedCharacteristic] "Characteristics were parsed incorrectly"
            Expect.sequenceEqual factors [expectedFactor] "Factors were parsed incorrectly"
            Expect.equal protocol expectedProtocol "Protocol was parsed incorrectly"

            let processV = processGetter m 0

            Expect.equal processV expectedProcess "Process was retrieved incorrectly"
    
        )
        testCase "ProcessGetterSampleParams" (fun () ->

            let headers = 
                sampleHeader @ characteristicHeaders @ factorHeaders @ parameterHeaders
                |> AnnotationTable.splitBySamples
                |> Seq.head

            let expectedOutput = ProcessOutput.Sample (Sample.create None (Some expectedSampleName) (Some [expectedCharacteristicValue]) (Some [expectedFactorValue]) None  )

            let expectedProtocol = Protocol.create None None None None None None (Some [expectedParameter]) None None

            let expectedProcess = Process.create None None (Some expectedProtocol) (Some [expectedParameterValue]) None None None None None (Some [expectedOutput]) None

            let characteristics,factors,protocol,processGetter = AnnotationTable.getProcessGetter Protocol.empty (headers |> AnnotationNode.splitIntoNodes)

            Expect.sequenceEqual characteristics [expectedCharacteristic] "Characteristics were parsed incorrectly"
            Expect.sequenceEqual factors [expectedFactor] "Factors were parsed incorrectly"
            Expect.equal protocol expectedProtocol "Protocol was parsed incorrectly"

            let processV = processGetter m 0

            Expect.equal processV expectedProcess "Process was retrieved incorrectly"
    
        )
    ]

[<Tests>]
let testProcessComparisonFunctions = 

    let parameters = 
        [
        ProtocolParameter.create None (Some (OntologyAnnotation.fromString "Term1" "" ""))
        ProtocolParameter.create None (Some (OntologyAnnotation.fromString "Term2" "" ""))
        ]

    let parameterValues1 =
        [
            ProcessParameterValue.create (Some parameters.[0]) (Value.fromOptions (Some "Value1") None None) None
            ProcessParameterValue.create (Some parameters.[1]) (Value.fromOptions (Some "Value2") None None) None
        ]

    let parameterValues2 = 
        [
            ProcessParameterValue.create (Some parameters.[0]) (Value.fromOptions (Some "Value1") None None) None
            ProcessParameterValue.create (Some parameters.[1]) (Value.fromOptions (Some "Value3") None None) None
        ]

    let characteristic =  MaterialAttribute.create None (Some (OntologyAnnotation.fromString "Term4" "" ""))
    let characteristicValue = MaterialAttributeValue.create None (Some characteristic) (Value.fromOptions (Some "Value4") None None) None

    let factor =  Factor.create None (Some "Factor") (Some (OntologyAnnotation.fromString "Term5" "" "")) None
    let factorValue = FactorValue.create None (Some factor) (Value.fromOptions (Some "Value5") None None) None


    let protocol1 = Protocol.create None (Some "Protocol1") None None None None (Some parameters) None None
    let protocol2 = Protocol.create None (Some "Protocol2") None None None None (Some parameters) None None

    let sample1 = Sample.create None (Some "Sample1") None None None
    let sample2 = Sample.create None (Some "Sample2") None None None
    let sample3 = Sample.create None (Some "Sample3") None None None
    let sample4 = Sample.create None (Some "Sample4") None None None

    let source1 = Source.create None (Some "Source1") None
    let source2 = Source.create None (Some "Source2") None
    let source3 = Source.create None (Some "Source3") None
    let source4 = Source.create None (Some "Source4") None

    testList "ProcessComparisonTests" [
        testCase "MergeProcesses" (fun () ->

            let process1 = Process.create None (Some "Process1") (Some protocol1) (Some parameterValues1) None None None None (Some [Source source1]) (Some [Sample sample1]) None
            let process2 = Process.create None (Some "Process2") (Some protocol1) (Some parameterValues1) None None None None (Some [Source source2]) (Some [Sample sample2]) None
            let process3 = Process.create None (Some "Process3") (Some protocol1) (Some parameterValues1) None None None None (Some [Source source3]) (Some [Sample sample3]) None
            let process4 = Process.create None (Some "Process4") (Some protocol1) (Some parameterValues1) None None None None (Some [Source source4]) (Some [Sample sample4]) None

            let mergedProcesses = AnnotationTable.mergeIdenticalProcesses [process1;process2;process3;process4]

            Expect.hasLength mergedProcesses 1 "Should have merged all 4 Processes as they execute with the same params"

            let mergedProcess = Seq.head mergedProcesses

            Expect.isSome mergedProcess.Inputs "Inputs were dropped"
            Expect.isSome mergedProcess.Outputs "Outputs were dropped"

            Expect.sequenceEqual mergedProcess.Inputs.Value [ProcessInput.Source source1;ProcessInput.Source source2;ProcessInput.Source source3;ProcessInput.Source source4] "Inputs were not merged correctly"
            Expect.sequenceEqual mergedProcess.Outputs.Value [ProcessOutput.Sample sample1;ProcessOutput.Sample sample2;ProcessOutput.Sample sample3;ProcessOutput.Sample sample4] "Inputs were not merged correctly"
        )        
        testCase "MergeProcessesDifferentParams" (fun () ->

            let process1 = Process.create None (Some "Process1") (Some protocol1) (Some parameterValues1) None None None None (Some [Source source1]) (Some [Sample sample1]) None
            let process2 = Process.create None (Some "Process2") (Some protocol1) (Some parameterValues1) None None None None (Some [Source source2]) (Some [Sample sample2]) None
            let process3 = Process.create None (Some "Process3") (Some protocol1) (Some parameterValues2) None None None None (Some [Source source3]) (Some [Sample sample3]) None
            let process4 = Process.create None (Some "Process4") (Some protocol1) (Some parameterValues2) None None None None (Some [Source source4]) (Some [Sample sample4]) None

            let mergedProcesses = AnnotationTable.mergeIdenticalProcesses [process1;process2;process3;process4]

            Expect.hasLength mergedProcesses 2 "Processes executed with two different parameter values, therefore they should be merged to two different processes"

            let mergedProcess1 = Seq.item 0 mergedProcesses
            let mergedProcess2 = Seq.item 1 mergedProcesses

            Expect.isSome mergedProcess1.Inputs "Inputs were dropped for mergedProcess1"
            Expect.isSome mergedProcess1.Outputs "Outputs were dropped for mergedProcess1"

            Expect.sequenceEqual mergedProcess1.Inputs.Value [ProcessInput.Source source1;ProcessInput.Source source2] "Inputs were not merged correctly for mergedProcess1"
            Expect.sequenceEqual mergedProcess1.Outputs.Value [ProcessOutput.Sample sample1;ProcessOutput.Sample sample2] "Inputs were not merged correctly for mergedProcess1"
        
            Expect.isSome mergedProcess2.Inputs "Inputs were dropped for mergedProcess2"
            Expect.isSome mergedProcess2.Outputs "Outputs were dropped for mergedProcess2"

            Expect.sequenceEqual mergedProcess2.Inputs.Value [ProcessInput.Source source3;ProcessInput.Source source4] "Inputs were not merged correctly for mergedProcess2"
            Expect.sequenceEqual mergedProcess2.Outputs.Value [ProcessOutput.Sample sample3;ProcessOutput.Sample sample4] "Inputs were not merged correctly for mergedProcess2"
        
        )
        testCase "MergeProcessesDifferentProtocols" (fun () ->

            let process1 = Process.create None (Some "Process1") (Some protocol1) (Some parameterValues1) None None None None (Some [Source source1]) (Some [Sample sample1]) None
            let process2 = Process.create None (Some "Process2") (Some protocol2) (Some parameterValues1) None None None None (Some [Source source2]) (Some [Sample sample2]) None
            let process3 = Process.create None (Some "Process3") (Some protocol1) (Some parameterValues1) None None None None (Some [Source source3]) (Some [Sample sample3]) None
            let process4 = Process.create None (Some "Process4") (Some protocol2) (Some parameterValues1) None None None None (Some [Source source4]) (Some [Sample sample4]) None

            let mergedProcesses = AnnotationTable.mergeIdenticalProcesses [process1;process2;process3;process4]

            Expect.hasLength mergedProcesses 2 "Processes executed with two different protocols, therefore they should be merged to two different processes"

            let mergedProcess1 = Seq.item 0 mergedProcesses
            let mergedProcess2 = Seq.item 1 mergedProcesses

            Expect.isSome mergedProcess1.Inputs "Inputs were dropped for mergedProcess1"
            Expect.isSome mergedProcess1.Outputs "Outputs were dropped for mergedProcess1"

            Expect.sequenceEqual mergedProcess1.Inputs.Value [ProcessInput.Source source1;ProcessInput.Source source3] "Inputs were not merged correctly for mergedProcess1"
            Expect.sequenceEqual mergedProcess1.Outputs.Value [ProcessOutput.Sample sample1;ProcessOutput.Sample sample3] "Inputs were not merged correctly for mergedProcess1"
        
            Expect.isSome mergedProcess2.Inputs "Inputs were dropped for mergedProcess2"
            Expect.isSome mergedProcess2.Outputs "Outputs were dropped for mergedProcess2"

            Expect.sequenceEqual mergedProcess2.Inputs.Value [ProcessInput.Source source2;ProcessInput.Source source4] "Inputs were not merged correctly for mergedProcess2"
            Expect.sequenceEqual mergedProcess2.Outputs.Value [ProcessOutput.Sample sample2;ProcessOutput.Sample sample4] "Inputs were not merged correctly for mergedProcess2"
        
        )
        testCase "IndexIdenticalProcessesByProtocolName" (fun () ->

            let process1 = Process.create None None (Some protocol1) (Some parameterValues1) None None None None (Some [Source source1]) (Some [Sample sample1]) None
            let process2 = Process.create None None (Some protocol1) (Some parameterValues2) None None None None (Some [Source source2]) (Some [Sample sample2]) None
            let process3 = Process.create None None (Some protocol2) (Some parameterValues1) None None None None (Some [Source source3]) (Some [Sample sample3]) None
            let process4 = Process.create None None (Some protocol2) (Some parameterValues2) None None None None (Some [Source source4]) (Some [Sample sample4]) None

            let indexedProcesses = AnnotationTable.indexRelatedProcessesByProtocolName [process1;process2;process3;process4]

            let names = indexedProcesses |> Seq.map (fun p -> Option.defaultValue "" p.Name)

            let expectedNames = ["Protocol1_0";"Protocol1_1";"Protocol2_0";"Protocol2_1"]

            Expect.sequenceEqual names expectedNames "Processes were not indexed correctly"
        )
        testCase "MergeSampleInfoTransformToSource" (fun () ->
            
            let sourceWithSampleName = ProcessInput.Source (Source.create None (Some "Sample1") None)

            let process1 = Process.create None None None None None None None None (Some [Source source1]) (Some [Sample sample1]) None
            let process2 = Process.create None None None None None None None None (Some [sourceWithSampleName]) (Some [Sample sample2]) None

            let updatedProcesses = AnnotationTable.updateSamplesByThemselves [process1;process2]

            let expectedProcessSequence =
                [
                    process1
                    Process.create None None None None None None None None (Some ([ProcessInput.Sample sample1])) (Some [Sample sample2]) None
                
                ]

            Expect.sequenceEqual updatedProcesses expectedProcessSequence "Source with same name as sample should have been converted to sample"        
        )
        testCase "MergeSampleInfo" (fun () ->
            
            let outputOfFirst = ProcessOutput.Sample (Sample.create None (Some "Sample1") None (Some [factorValue]) None)
            let inputOfSecond = ProcessInput.Source (Source.create None (Some "Sample1") (Some [characteristicValue]))

            let process1 = Process.create None None None None None None None None (Some [Source source1]) (Some [outputOfFirst]) None
            let process2 = Process.create None None None None None None None None (Some [inputOfSecond]) (Some [Sample sample2]) None

            let updatedProcesses = AnnotationTable.updateSamplesByReference [process1;process2] [process1;process2]


            let outputs = (Seq.head updatedProcesses).Outputs
            Expect.isSome outputs "Outputs of first Process are empty"
            let outputSample = 
                match outputs.Value with
                | [ProcessOutput.Sample s] -> s
                | _ -> failwithf "Expected a single sample for outputs but got %A" outputs


            let inputs = (Seq.item 1 updatedProcesses).Inputs
            Expect.isSome inputs "Inputs of second Process are empty"
            let inputSample = 
                match inputs.Value with
                | [ProcessInput.Sample s] -> s
                | _ -> failwithf "Expected a single sample for inputs but got %A" inputs
            
            let expectedSample = Sample.create None (Some "Sample1") (Some [characteristicValue]) (Some [factorValue]) None

            Expect.equal inputSample outputSample "The information of the output of the first process and the input of the second process was not equalized"      
            Expect.equal outputSample expectedSample "Values were not correctly merged"
        )
    ]


[<Tests>]
let testMetaDataFunctions = 

    let sourceDirectory = __SOURCE_DIRECTORY__ + @"/TestFiles/"
    let referenceAssayFilePath = System.IO.Path.Combine(sourceDirectory,"AssayMetadataTestFile.xlsx")

    testList "MetaDataTests" [
        testCase "CanReadMetaData" (fun () ->

            let doc = Spreadsheet.fromFile referenceAssayFilePath false

            let sst = Spreadsheet.getSharedStringTable doc

            let rows = 
                Spreadsheet.tryGetSheetBySheetName "Investigation" doc
                |> Option.get
                |> SheetData.getRows
                |> Seq.map (Row.mapCells (Cell.includeSharedStringValue sst))

            let readingSuccess = 
                try 
                    AssayFile.MetaData.fromRows rows |> ignore
                    Result.Ok "DidRun"
                with
                | err -> Result.Ok(sprintf "Reading the test metadata failed: %s" err.Message)

            Expect.isOk readingSuccess (Result.getMessage readingSuccess)
        )
        testCase "ReadsMetaDataCorrectly" (fun () ->

            let doc = Spreadsheet.fromFile referenceAssayFilePath false

            let sst = Spreadsheet.getSharedStringTable doc

            let rows = 
                Spreadsheet.tryGetSheetBySheetName "Investigation" doc
                |> Option.get
                |> SheetData.getRows
                |> Seq.map (Row.mapCells (Cell.includeSharedStringValue sst))

            let assay,contacts = AssayFile.MetaData.fromRows rows 

            let testAssay = Assays.fromString "protein expression profiling" "http://purl.obolibrary.org/obo/OBI_0000615" "OBI" "mass spectrometry" "" "OBI" "iTRAQ" "" []

            let testContact = Contacts.fromString "Leo" "Zeef" "A" "" "" "" "Oxford Road, Manchester M13 9PT, UK" "Faculty of Life Sciences, Michael Smith Building, University of Manchester" "author" "" "" [Comment.fromString "Worksheet" "Sheet3"]
                
            Expect.isSome assay "Assay metadata information could not be read from metadata sheet"

            Expect.equal assay.Value testAssay "Assay metadata information could not be correctly read from metadata sheet"

            Expect.hasLength contacts 3 "Wrong count of parsed contacts"

            Expect.equal contacts.[2] testContact "Test Person could not be correctly read from metadata sheet"
        )

    ]
    |> testSequenced

[<Tests>]
let testAssayFileReader = 

    let sourceDirectory = __SOURCE_DIRECTORY__ + @"/TestFiles/"
    let assayFilePath = System.IO.Path.Combine(sourceDirectory,"AssayTestFile.xlsx")

    let expectedPersons =
        [
        Contacts.fromString "Weil" "Lukas" "H." "" "" "" "" "" "researcher" "http://purl.obolibrary.org/obo/MS_1001271" "MS" ([Comment.fromString "Worksheet" "GreatAssay"])
        Contacts.fromString "Leil" "Wukas" "" "" "" "" "" "" "" "" "" ([Comment.fromString "Worksheet" "SecondAssay"])
        ]

    let technologyType =
        OntologyAnnotation.fromString "mass spectrometry" "http://purl.obolibrary.org/obo/MS_1000268" "MS"
        
    let fileName = @"GreatAssay\assay.isa.xlsx"

    let temperatureUnit = ProtocolParameter.fromString "temperature unit" "0000005" "UO" 

    let temperature = ProtocolParameter.fromString "temperature" "0000029" "NCRO" 

    let peptidase = ProtocolParameter.fromString "Peptidase" "C16965" "NCIT"

    let time1 = ProtocolParameter.fromString "time" "0000721" "EFO"

    let time2 = Factor.fromString "time" "time" "0000165" "PATO"

    let leafSize = MaterialAttribute.fromString "leaf size" "0002637" "TO"

    testList "AssayFileReaderTests" [
        testCase "ReaderSuccess" (fun () -> 
                       
            let readingSuccess = 
                try 
                    AssayFile.fromFile assayFilePath |> ignore
                    Result.Ok "DidRun"
                with
                | err -> Result.Error(sprintf "Reading the test file failed: %s" err.Message)

            Expect.isOk readingSuccess (Result.getMessage readingSuccess)
        )
        testCase "ReadsCorrectly" (fun () ->        
            
            let factors,protocols,persons,assay = AssayFile.fromFile assayFilePath

            let expectedProtocols = 
                [
                Protocol.create None (Some "GreatAssay") None None None None (Some [temperatureUnit]) None None
                Protocol.create None (Some "peptide_digestion") None None None None (Some [peptidase;temperature;time1]) None None
                Protocol.create None (Some "SecondAssay") None None None None (Some [temperatureUnit]) None None
                ]

            let expectedFactors = [time2]

            Expect.sequenceEqual factors expectedFactors        "Factors were read incorrectly"
            Expect.sequenceEqual protocols expectedProtocols    "Protocols were read incorrectly"
            Expect.sequenceEqual persons expectedPersons        "Persons were read incorrectly from metadata sheet"

            Expect.isSome assay.FileName "FileName was not read"
            Expect.equal assay.FileName.Value fileName "FileName was not read correctly"

            Expect.isSome assay.TechnologyType "Technology Type was not read"
            Expect.equal assay.TechnologyType.Value technologyType "Technology Type was not read correctly"

            Expect.isSome assay.CharacteristicCategories "Characteristics were not read"
            Expect.equal assay.CharacteristicCategories.Value [leafSize] "Characteristics were not read correctly"

            Expect.isSome assay.ProcessSequence "Processes were not read"
            assay.ProcessSequence.Value
            |> Seq.map (fun p -> Option.defaultValue "" p.Name)
            |> fun names -> Expect.sequenceEqual names ["GreatAssay_0";"GreatAssay_1";"peptide_digestion_0";"SecondAssay_0"] "Process names do not match"

        )
    ]