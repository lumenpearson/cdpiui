using System.ComponentModel;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.UI;

public class LanguageSelectModelTests
{
    [Fact]
    public void DisplayNameChangesNotifyBindingsWithTheUpdatedValueAndStableId()
    {
        var item = new LanguageSelectModel { Id = "en-us", DisplayName = "English" };
        var changes = new List<(object? Sender, string? Property, string Value, string Id)>();
        var observable = Assert.IsAssignableFrom<INotifyPropertyChanged>(item);
        observable.PropertyChanged += (sender, args) =>
            changes.Add((sender, args.PropertyName, item.DisplayName, item.Id));

        item.DisplayName = "Английский (English)";
        item.DisplayName = "English";

        Assert.Equal(2, changes.Count);
        Assert.All(changes, change =>
        {
            Assert.Same(item, change.Sender);
            Assert.Equal(nameof(LanguageSelectModel.DisplayName), change.Property);
            Assert.Equal("en-us", change.Id);
        });
        Assert.Equal("Английский (English)", changes[0].Value);
        Assert.Equal("English", changes[1].Value);
    }

    [Fact]
    public void ReapplyingTheCurrentDisplayNameDoesNotPublishAnotherChange()
    {
        var item = new LanguageSelectModel { Id = "ru", DisplayName = "Русский (Russian)" };
        int changes = 0;
        var observable = Assert.IsAssignableFrom<INotifyPropertyChanged>(item);
        observable.PropertyChanged += (_, _) => changes++;

        item.DisplayName = new string(item.DisplayName.ToCharArray());

        Assert.Equal(0, changes);
        Assert.Equal("ru", item.Id);
        Assert.Equal("Русский (Russian)", item.DisplayName);
    }
}
