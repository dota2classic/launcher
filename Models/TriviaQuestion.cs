using System.Text.Json.Serialization;

namespace d2c_launcher.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ItemRecipeQuestion), "item_recipe")]
[JsonDerivedType(typeof(MultipleChoiceQuestion), "multiple_choice")]
public abstract class TriviaQuestion
{
    public string Id { get; set; } = "";
}

public sealed class ItemRecipeQuestion : TriviaQuestion
{
    public string TargetItem { get; set; } = "";
    public string[] Ingredients { get; set; } = [];
    public string[] Distractors { get; set; } = [];
}

public sealed class MultipleChoiceQuestion : TriviaQuestion
{
    public string Question { get; set; } = "";
    public string[] Answers { get; set; } = [];
    public int CorrectIndex { get; set; }
}
