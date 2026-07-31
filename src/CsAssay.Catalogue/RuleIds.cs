namespace CsAssay.Catalogue;

public static class RuleIds
{
    public const string UnauthorizedSuppression = "CSAP0001";
    public const string NullableDisabled = "CSAN0001";
    public const string NullForgiving = "CSAN0002";
    public const string NullValueIntroduction = "CSAN0003";
    public const string NullableCoreContract = "CSAN0004";
    public const string MutableSetter = "CSAI0001";
    public const string MutableCollectionExposure = "CSAI0002";
    public const string MutableShellLeakage = "CSAI0003";
    public const string SwallowedException = "CSAE0001";
    public const string CoreBoundaryException = "CSAE0002";
    public const string FunctionCandidate = "CSAF0001";
    public const string LoopPipelineOpportunity = "CSAF0002";
    public const string PrimitiveObsession = "CSAD0001";
    public const string StateFlags = "CSAD0002";
    public const string AsyncVoid = "CSAA0001";
    public const string BlockingAsync = "CSAA0002";
    public const string ExtensibleClosedHierarchy = "CSAU0001";
    public const string IncompleteClosedHierarchySwitch = "CSAU0002";
    public const string UnguardedOneOfExtraction = "CSAU0003";
    public const string NativeUnionDiscard = "CSAU0004";
}
