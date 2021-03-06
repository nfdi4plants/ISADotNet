namespace ISADotNet.API

open ISADotNet
open Update

module Investigation =

    /// Returns contacts of an investigation
    let getContacts (investigation : Investigation) =
        investigation.Contacts

    /// Applies function f on person of an investigation
    let mapContacts (f:Person list -> Person list) (investigation: Investigation) =
        { investigation with 
            Contacts = Option.mapDefault [] f investigation.Contacts }

    /// Replaces persons of an investigation with the given person list
    let setContacts (investigation:Investigation) (persons:Person list) =
        { investigation with
            Contacts = Some persons }

    /// Returns publications of an investigation
    let getPublications (investigation : Investigation) =
        investigation.Publications

    /// Applies function f on publications of an investigation
    let mapPublications (f:Publication list -> Publication list) (investigation: Investigation) =
        { investigation with 
            Publications = Option.mapDefault [] f investigation.Publications }

    /// Replaces publications of an investigation with the given publication list
    let setPublications (investigation:Investigation) (publications:Publication list) =
        { investigation with
            Publications = Some publications }

    /// Returns ontology source ref of an investigation
    let getOntologies (investigation : Investigation) =
        investigation.OntologySourceReferences

    /// Applies function f on ontology source ref of an investigation
    let mapOntologies (f:OntologySourceReference list -> OntologySourceReference list) (investigation: Investigation) =
        { investigation with 
            OntologySourceReferences = Option.mapDefault []  f investigation.OntologySourceReferences }

    /// Replaces ontology source ref of an investigation with the given ontology source ref list
    let setOntologies (investigation:Investigation) (ontologies:OntologySourceReference list) =
        { investigation with
            OntologySourceReferences = Some ontologies }

    /// Returns studies of an investigation
    let getStudies (investigation : Investigation) =
        investigation.Studies

    /// Applies function f on studies of an investigation
    let mapStudies (f:Study list -> Study list) (investigation: Investigation) =
        { investigation with 
            Studies = Option.mapDefault []  f investigation.Studies }

    /// Replaces studies of an investigation with the given study list
    let setStudies (investigation:Investigation) (studies:Study list) =
        { investigation with
            Studies = Some studies }

    /// Returns comments of an investigation
    let getComments (investigation : Investigation) =
        investigation.Comments

    /// Applies function f on comments of an investigation
    let mapComments (f:Comment list -> Comment list) (investigation: Investigation) =
        { investigation with 
            Comments = Option.mapDefault [] f investigation.Comments }

    /// Replaces comments of an investigation with the given comment list
    let setComments (investigation:Investigation) (comments:Comment list) =
        { investigation with
            Comments = Some comments }

    /// Returns remarks of an investigation
    let getRemarks (investigation : Investigation) =
        investigation.Remarks

    /// Applies function f on remarks of an investigation
    let mapRemarks (f:Remark list -> Remark list) (investigation: Investigation) =
        { investigation with 
            Remarks = f investigation.Remarks }

    /// Replaces remarks of an investigation with the given remark list
    let setRemarks (investigation:Investigation) (remarks:Remark list) =
        { investigation with
            Remarks = remarks }

    /// Update the investigation with the values of the given newInvestigation
    let update (updateOption:API.Update.UpdateOptions) (investigation : Investigation) newInvestigation =
        updateOption.updateRecordType investigation newInvestigation