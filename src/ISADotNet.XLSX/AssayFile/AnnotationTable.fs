﻿namespace ISADotNet.XLSX.AssayFile

open ISADotNet.XLSX
open ISADotNet

/// Functions for parsing an annotation table to the described processes
module AnnotationTable = 
    
    /// Splits the headers of an annotation table into parts, so that each part has at most one input and one output column (Source Name, Sample Name)
    let splitBySamples (headers : seq<string>) =
        let isSample header = AnnotationColumn.tryParseSampleName header |> Option.isSome 
        let isSource header = AnnotationColumn.tryParseSourceName header |> Option.isSome 
        
        match Seq.filter isSource headers |> Seq.length, Seq.filter isSample headers |> Seq.length with
        | 1,1  -> 
            Seq.filter isSample headers
            |> Seq.append (Seq.filter (fun s -> (isSample s || isSource s) |> not) headers) 
            |> Seq.append (Seq.filter isSource headers)
            |> Seq.singleton
        | 0,1 -> Seq.append (Seq.filter (isSample>>not) headers) (Seq.filter isSample headers) |> Seq.singleton
        | 0,2 when Seq.head headers |> isSample && Seq.last headers |> isSample -> headers |> Seq.singleton
        | _ -> Seq.groupWhen true (fun header -> isSample header || isSource header) headers

    /// Splits the parts into protocols according to the headers given together with the named protocols. Assins the input and output column to each resulting protocol
    let splitByNamedProtocols (namedProtocols : (Protocol * seq<string>) seq) (headers : seq<string>) =
        let sortAgainst =
            let m = headers |> Seq.mapi (fun i x -> x,i) |> Map.ofSeq
            fun hs -> hs |> Seq.sortBy (fun v -> m.[v])
        let isSample (header:string) = AnnotationColumn.isSample header || AnnotationColumn.isSource header

        let rec loop (protocolOverlaps : (Protocol * seq<string>) list) (namedProtocols : (Protocol * Set<string>) list) (remainingHeaders : Set<string>) =
            match namedProtocols with
            | _ when remainingHeaders.IsEmpty -> 
                protocolOverlaps
            | (p,hs)::l ->
                if Set.isSubset hs remainingHeaders then
                    loop ((p,Set.toSeq hs)::protocolOverlaps) l (Set.difference remainingHeaders hs)
                else 
                    loop protocolOverlaps l remainingHeaders
            | [] ->
                (Protocol.empty ,remainingHeaders |> Set.toSeq)::protocolOverlaps
        
        let sampleColumns,otherColumns = headers |> Seq.filter (isSample) |> Seq.toList,headers |> Seq.filter (isSample>>not)
    
        let protocolOverlaps = 
            loop [] (namedProtocols |> Seq.map (fun (p,hs) -> p,hs |> Set.ofSeq) |> List.ofSeq) (otherColumns |> Set.ofSeq)
            |> Seq.map (fun (p,hs) -> p, sortAgainst hs)
        
        match sampleColumns with
        | [] ->         protocolOverlaps 
        | [s] ->        protocolOverlaps |> Seq.map (fun (p,hs) -> p,Seq.append [s] hs)
        | [s1;s2] ->    protocolOverlaps |> Seq.map (fun (p,hs) -> p,Seq.append (Seq.append [s1] hs) [s2])
        | s ->          protocolOverlaps |> Seq.map (fun (p,hs) -> p,Seq.append hs s)

    /// Name unnamed protocols with the given sheetName. If there is more than one unnamed protocol, additionally add an index
    let indexProtocolsBySheetName (sheetName:string) (protocols : (Protocol * seq<string>) seq) =
        let unnamedProtocolCount = protocols |> Seq.filter (fun (p,_) -> p.Name.IsNone) |> Seq.length
        match unnamedProtocolCount with
        | 0 -> protocols
        | 1 -> 
            protocols 
            |> Seq.map (fun (p,hs) -> 
                if p.Name.IsNone then
                    {p with Name = Some sheetName},hs
                else p,hs
            )
        | _ -> 
            let mutable i = 0 
            protocols 
            |> Seq.map (fun (p,hs) -> 
                if p.Name.IsNone then
                    let name = sprintf "%s_%i" sheetName i
                    i <- i + 1
                    {p with Name = Some name},hs
                else p,hs
            )

    /// Returns the protocol described by the headers and a function for parsing the values of the matrix to the processes of this protocol
    let getProcessGetter protocolMetaData (nodes : seq<seq<string>>) =
    
        let characteristics,characteristicValueGetters =
            nodes |> Seq.choose AnnotationNode.tryGetCharacteristicGetter
            |> Seq.fold (fun (cl,cvl) (c,cv) -> c.Value :: cl, cv :: cvl) ([],[])
            |> fun (l1,l2) -> List.rev l1, List.rev l2
        let factors,factorValueGetters =
            nodes |> Seq.choose AnnotationNode.tryGetFactorGetter
            |> Seq.fold (fun (fl,fvl) (f,fv) -> f.Value :: fl, fv :: fvl) ([],[])
            |> fun (l1,l2) -> List.rev l1, List.rev l2
        let parameters,parameterValueGetters =
            nodes |> Seq.choose AnnotationNode.tryGetParameterGetter
            |> Seq.fold (fun (pl,pvl) (p,pv) -> p.Value :: pl, pv :: pvl) ([],[])
            |> fun (l1,l2) -> List.rev l1, List.rev l2
    
        let dataFileGetter = nodes |> Seq.tryPick AnnotationNode.tryGetDataFileGetter

        let inputGetter,outputGetter =
            match nodes |> Seq.tryPick AnnotationNode.tryGetSourceNameGetter with
            | Some inputNameGetter ->
                let outputNameGetter = nodes |> Seq.tryPick AnnotationNode.tryGetSampleNameGetter
                let inputGetter = 
                    fun matrix i -> 
                        let source = 
                            Source.create
                                None
                                (inputNameGetter matrix i)
                                (characteristicValueGetters |> List.map (fun f -> f matrix i) |> API.Option.fromValueWithDefault [])
                        if dataFileGetter.IsSome then 
                            [source;source]
                        else 
                            [source]
                
                let outputGetter =
                    fun matrix i -> 
                        let data = dataFileGetter |> Option.map (fun f -> f matrix i)
                        let outputName = 
                            match outputNameGetter |> Option.bind (fun o -> o matrix i) with
                            | Some s -> Some s
                            | None -> 
                                match data with
                                | Some data -> data.Name
                                | None -> None
                        let sample =
                            Sample.create
                                None
                                outputName
                                (characteristicValueGetters |> List.map (fun f -> f matrix i) |> API.Option.fromValueWithDefault [])
                                (factorValueGetters |> List.map (fun f -> f matrix i) |> API.Option.fromValueWithDefault [])
                                (inputGetter matrix i |> List.distinct |> Some)
                        if data.IsSome then 
                            [ProcessOutput.Sample sample; ProcessOutput.Data data.Value]
                        else 
                            [ProcessOutput.Sample sample]                      
                (fun matrix i -> inputGetter matrix i |> List.map Source |> Some),outputGetter
            | None ->
                let inputNameGetter = nodes |> Seq.head |> AnnotationNode.tryGetSampleNameGetter
                let outputNameGetter = nodes |> Seq.last |> AnnotationNode.tryGetSampleNameGetter
                let inputGetter = 

                    fun matrix i ->      
                        let source = 
                            inputNameGetter
                            |> Option.map (fun ing ->
                                Sample.create
                                    None
                                    (ing matrix i)
                                    (characteristicValueGetters |> List.map (fun f -> f matrix i) |> API.Option.fromValueWithDefault [])
                                    None
                                    None
                                |> ProcessInput.Sample
                            )   
                        match source with
                        | Some source when dataFileGetter.IsSome -> Some [source;source]
                        | Some source -> Some  [source]
                        | None -> None
                            

                let outputGetter =
                    fun matrix i -> 
                        let data = dataFileGetter |> Option.map (fun f -> f matrix i)
                        let outputName = 
                            match outputNameGetter |> Option.bind (fun o -> o matrix i) with
                            | Some s -> Some s
                            | None -> 
                                match data with
                                | Some data -> data.Name
                                | None -> None
                        let sample =
                            Sample.create
                                None
                                outputName
                                (characteristicValueGetters |> List.map (fun f -> f matrix i) |> API.Option.fromValueWithDefault [])
                                (factorValueGetters |> List.map (fun f -> f matrix i) |> API.Option.fromValueWithDefault [])
                                None
                        if data.IsSome then 
                            [ProcessOutput.Sample sample; ProcessOutput.Data data.Value]
                        else 
                            [ProcessOutput.Sample sample]  
                inputGetter,outputGetter
    
        let protocol = {protocolMetaData with Parameters = API.Option.fromValueWithDefault [] parameters}
    
        characteristics,
        factors,
        protocol,
        fun (matrix : System.Collections.Generic.Dictionary<(string * int),string>) i ->
            Process.create 
                None 
                None 
                (Some protocol) 
                (parameterValueGetters |> List.map (fun f -> f matrix i) |> API.Option.fromValueWithDefault [])
                None
                None
                None
                None          
                (inputGetter matrix i)
                (outputGetter matrix i |> Some)
                None

    /// Merges processes with the same parameter values, grouping the input and output files
    let mergeIdenticalProcesses (processes : seq<Process>) =
        processes
        |> Seq.groupBy (fun p -> p.ExecutesProtocol,p.ParameterValues)
        |> Seq.map (fun (_,processGroup) ->
            processGroup
            |> Seq.reduce (fun p1 p2 ->
                let mergedInputs = List.append (p1.Inputs |> Option.defaultValue []) (p2.Inputs |> Option.defaultValue []) |> API.Option.fromValueWithDefault []
                let mergedOutputs = List.append (p1.Outputs |> Option.defaultValue []) (p2.Outputs |> Option.defaultValue []) |> API.Option.fromValueWithDefault []
                {p1 with Inputs = mergedInputs; Outputs = mergedOutputs}
            )
        )

    /// Name processes by the protocol they execute. If more than one process executes the same protocol, additionally add an index
    let indexRelatedProcessesByProtocolName (processes : seq<Process>) =
        processes
        |> Seq.groupBy (fun p -> p.ExecutesProtocol)
        |> Seq.collect (fun (protocol,processGroup) ->
            processGroup
            |> Seq.mapi (fun i p -> 
                {p with Name =                         
                        protocol.Value.Name |> Option.map (fun s -> sprintf "%s_%i" s i)
                }
            )
        )    

    /// Create a sample from a source
    let sampleOfSource (s:Source) =
        Sample.create s.ID s.Name s.Characteristics None None

    /// Create a sample from a source
    let sourceOfSample (s:Sample) =
        Source.create s.ID s.Name s.Characteristics


    /// Updates the sample information in the given processes with the information of the samples in the given referenceProcesses.
    ///
    /// If the processes contain a source with the same name as a sample in the referenceProcesses. Additionally transforms it to a sample
    let private updateSamplesBy (referenceProcesses : Process seq) (processes : Process seq) = 
        let samples = 
            referenceProcesses
            |> Seq.collect (fun p -> 
                let inputs =
                    p.Inputs 
                    |> Option.defaultValue [] 
                    |> Seq.choose (function | ProcessInput.Sample x -> Some(x.Name,true, x) | ProcessInput.Source x -> Some (x.Name,false,sampleOfSource x)| _ -> None)
                let outputs =
                    p.Outputs 
                    |> Option.defaultValue [] 
                    |> Seq.choose (function | ProcessOutput.Sample x -> Some(x.Name,true, x) | _ -> None)
                Seq.append inputs outputs
                |> Seq.distinct
                )
            |> Seq.filter (fun (name,_,samples) -> name <> None && name <> (Some ""))
            |> Seq.groupBy (fun (name,_,samples) -> name)
            |> Seq.map (fun (name,samples) -> 
                let aggregatedSample = 
                    samples 
                    |> Seq.map (fun (name,_,s) -> s) 
                    |> Seq.reduce (fun s1 s2 -> if s1 = s2 then s1 else API.Update.UpdateByExistingAppendLists.updateRecordType s1 s2)
                if Seq.exists (fun (name,isSample,s) -> isSample) samples then
                    name, ProcessInput.Sample aggregatedSample
                else name, ProcessInput.Source (sourceOfSample aggregatedSample)          
            )
            |> Map.ofSeq
    
        let updateInput (i:ProcessInput) =
            match i with 
            | ProcessInput.Source x ->      match Map.tryFind x.Name samples with   | Some s -> s | None -> ProcessInput.Source x
            | ProcessInput.Sample x ->      match Map.tryFind x.Name samples with   | Some s -> s | None -> ProcessInput.Sample x
            | ProcessInput.Data x ->        ProcessInput.Data x
            | ProcessInput.Material x ->    ProcessInput.Material x
        let updateOutput (o:ProcessOutput) =                         
            match o with                                             
            | ProcessOutput.Sample x ->     match Map.tryFind x.Name samples with   | Some (ProcessInput.Sample x) -> ProcessOutput.Sample x | _ -> ProcessOutput.Sample x
            | ProcessOutput.Data x ->       ProcessOutput.Data x
            | ProcessOutput.Material x ->   ProcessOutput.Material x
        processes
        |> Seq.map (fun p -> 
           {p with
                    Inputs = p.Inputs |> Option.map (List.map updateInput)
                    Outputs = p.Outputs |> Option.map (List.map updateOutput)
           }
        )

    /// Updates the sample information in the given processes with the information of the samples in the given referenceProcesses.
    ///
    /// If the processes contain a source with the same name as a sample in the referenceProcesses. Additionally transforms it to a sample
    let updateSamplesByReference (referenceProcesses : Process seq) (processes : Process seq) = 
        referenceProcesses
        |> Seq.append processes
        |> updateSamplesBy processes

    /// Updates the sample information in the given processes with the information of the samples in the given referenceProcesses.
    ///
    /// If the processes contain a source with the same name as a sample in the referenceProcesses. Additionally transforms it to a sample
    let updateSamplesByThemselves (processes : Process seq) =
        processes
        |> updateSamplesBy processes
