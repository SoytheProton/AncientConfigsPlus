using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;

namespace AncientConfigsPlus.AncientConfigsPlusCode;

public static class ExceptionAncientsExtension
{
    internal static readonly Dictionary<ModelId, ModelId[]> ActSpecificAncients = [];
    
    internal static readonly List<ModelId> CompleteExceptionAncients = [];
    
    /// <summary>
    /// Using an interop you can call this extension in the constructor of your CustomAncientModel.<br/>
    /// This will add it to the "exception" list where it will do unique behavior to try and best replicate act specific ancient conditions.<br/>
    /// This is for the incomplete exception list,  where the mod will still attempt to handle weighting and disabling.
    /// If you want to avoid this behavior, use <see cref="AddCompleteExceptionToAncientList"/>.
    /// </summary>
    /// <param name="ancientModel">The ancient model you are adding into the exception list.</param>
    /// <param name="actIds">The ModelIds of the acts that this Ancient can be spawned in. You need to use <see cref="ModelDb.GetId"/> instead of <see cref="ModelDb.Act"/> due to Ancients loading in before Acts. <br/>
    /// Note: this will also be used to determine what other ancients are rolled when an exception is rolled. </param>
    public static void AddActSpecificAncientList(this CustomAncientModel ancientModel, ModelId[] actIds)
    {
        if (ActSpecificAncients.ContainsKey(ancientModel.Id))
        {
            MainFile.Logger.Warn($"Attempted to add {ancientModel.Title.GetFormattedText()} to Exception List multiple times.");
        }

        ActSpecificAncients[ancientModel.Id] = actIds;
    }
    
    /// <summary>
    /// Using an interop you can call this extension in the constructor of your CustomAncientModel.<br/>
    /// Using this extension means that the mod will <b>completely</b> ignore all logic when this Ancient is rolled.<br/>
    /// <i>(Best used when an Ancient has very specific or unique spawn conditions.)</i>
    /// </summary>
    /// <param name="ancientModel">The ancient model you are adding into the complete exception list.</param>
    public static void AddCompleteExceptionToAncientList(this CustomAncientModel ancientModel)
    {
        if (CompleteExceptionAncients.Contains(ancientModel.Id))
        {
            MainFile.Logger.Warn($"Attempted to add {ancientModel.Title.GetFormattedText()} to Exception List multiple times.");
        }
        else
        {
            CompleteExceptionAncients.Add(ancientModel.Id);
        }
    }
}