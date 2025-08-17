namespace Omny.Cms.Editor;

public record FieldContent(string FieldType, string Content);
public record CollectionFieldContent(IList<FieldContent> Items);
