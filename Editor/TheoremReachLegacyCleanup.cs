using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheoremReachSDK.Editor
{
    // Offers to remove what a TheoremReach .unitypackage (3.7.0 and earlier) left under Assets. A .unitypackage never
    // deletes anything, so each upgrade kept every earlier AAR, framework and script beside the new ones. They conflict
    // with this package: their Android libraries duplicate the ones the External Dependency Manager adds, and their
    // scripts and frameworks duplicate the package's own.
    //
    // The assets are found by the GUIDs those packages gave them, wherever the game moved them. Shared folders such as
    // Assets/Plugins, and files a game may have made its own (a sample scene, Unity IAP's BillingMode.json), aren't in
    // the lists. Removed assets go to the trash, so they can come back.
    [InitializeOnLoad]
    internal static class TheoremReachLegacyCleanup
    {
        private const string Title = "TheoremReach";
        private const string AskedThisSession = "TheoremReach.LegacyCleanup.Asked";

        static TheoremReachLegacyCleanup()
        {
            EditorApplication.delayCall += OfferOncePerSession;
        }

        private static void OfferOncePerSession()
        {
            if (SessionState.GetBool(AskedThisSession, false)) return;
            SessionState.SetBool(AskedThisSession, true);
            Offer(fromMenu: false);
        }

        [MenuItem("Tools/TheoremReach/Remove Old TheoremReach Files")]
        private static void OfferFromMenu()
        {
            Offer(fromMenu: true);
        }

        private static void Offer(bool fromMenu)
        {
            List<string> oldAssets = Existing(OldAssetGuids);
            if (oldAssets.Count == 0)
            {
                if (fromMenu) EditorUtility.DisplayDialog(Title, "No files from an older TheoremReach package are left.", "OK");
                return;
            }

            string list = string.Join("\n", oldAssets);
            // A build machine has no one to ask.
            if (Application.isBatchMode)
            {
                Debug.LogWarning("TheoremReach: these files from an older TheoremReach package conflict with this one. " +
                                 "Remove them from the project:\n" + list);
                return;
            }

            const int Shown = 25;
            string shownList = string.Join("\n", oldAssets.Take(Shown)) +
                               (oldAssets.Count > Shown ? "\n...and " + (oldAssets.Count - Shown) + " more" : "");
            bool remove = EditorUtility.DisplayDialog(Title,
                oldAssets.Count + " files from an older TheoremReach package are still under Assets, and conflict with " +
                "this one. Move them to the trash?\n\n" + shownList,
                "Move to Trash", "Not Now");
            if (!remove)
            {
                Debug.LogWarning("TheoremReach: left these files from an older TheoremReach package, which conflict " +
                                 "with this one. Tools > TheoremReach > Remove Old TheoremReach Files offers again:\n" + list);
                return;
            }

            var failed = new List<string>();
            AssetDatabase.MoveAssetsToTrash(oldAssets.ToArray(), failed);
            RemoveEmptyFolders();
            if (failed.Count > 0)
            {
                Debug.LogError("TheoremReach: couldn't move these to the trash; remove them by hand:\n" +
                               string.Join("\n", failed));
            }
            else
            {
                Debug.Log("TheoremReach: moved " + oldAssets.Count + " files from an older TheoremReach package to the " +
                          "trash:\n" + list);
            }
        }

        // Only paths under Assets: the GUIDs are the old packages', so a match in Packages would be another package's.
        private static List<string> Existing(IEnumerable<string> guids)
        {
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/") && (File.Exists(path) || Directory.Exists(path)))
                .Distinct()
                .OrderBy(path => path)
                .ToList();
        }

        // Deepest first, so a parent empties once its children go.
        private static void RemoveEmptyFolders()
        {
            foreach (string folder in Existing(OldFolderGuids).OrderByDescending(path => path.Length))
            {
                if (AssetDatabase.IsValidFolder(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    AssetDatabase.MoveAssetToTrash(folder);
                }
            }
        }

        private static readonly string[] OldAssetGuids =
        {
            "850f8d84ecce44a8869361652192395f", // Assets/PlayServicesResolver
            "18112f9fbcaab495daf34c417dd6db1e", // Assets/Plugins/Android/activity-1.1.0.aar
            "3266240df7cc3435195b1df3b44d090a", // Assets/Plugins/Android/animated-vector-drawable-23.4.0.aar
            "8bde3733c18de4743bf0685f7bf4a74c", // Assets/Plugins/Android/annotation-1.1.0.jar
            "dcfdfb6f817d24c66b13398395057bc4", // Assets/Plugins/Android/appcompat-1.1.0.aar
            "45e15e14db79e4d7080561903e31733d", // Assets/Plugins/Android/appcompat-1.2.0.aar
            "aff4eab2c91ba45d4b4b40df2d7aa58a", // Assets/Plugins/Android/appcompat-resources-1.1.0.aar
            "4247dfc775bb849dd95e2a11679e932e", // Assets/Plugins/Android/appcompat-resources-1.2.0.aar
            "524cdba04613d46f49f7dd9957439aed", // Assets/Plugins/Android/appcompat-v7-23.4.0.aar
            "14c2aeb71a477478e93d88bb1b286576", // Assets/Plugins/Android/asynclayoutinflater-1.0.0.aar
            "42e8b0a8759b547f99675639aef67d6f", // Assets/Plugins/Android/browser-1.0.0.aar
            "d00fee6e9545c4307818fc2e8bd98574", // Assets/Plugins/Android/collection-1.1.0.jar
            "abe232aa0b6ee45be9c7a1b640030501", // Assets/Plugins/Android/core-1.1.0.aar
            "8531cb32ff77e402086dfe1cc5b8fd62", // Assets/Plugins/Android/core-1.3.0.aar
            "0f6cffb9f9ca74f7c84daece310f6477", // Assets/Plugins/Android/core-common-2.1.0.jar
            "bb594fb19c56b45f59db6c7a909fad1d", // Assets/Plugins/Android/core-runtime-2.1.0.aar
            "8922cbcf4b4804d8490dcd0c7a84d1be", // Assets/Plugins/Android/cursoradapter-1.0.0.aar
            "90ddd32d15fbf48d2989c815d726dc65", // Assets/Plugins/Android/customview-1.0.0.aar
            "9450b94c97b684d71a6fbf9a7084e22f", // Assets/Plugins/Android/firebase-common-9.0.1
            "dff24042354fd488ea0fea49b4917d49", // Assets/Plugins/Android/fragment-1.2.4.aar
            "eea1267b79187445c878a9dc2e52f9b5", // Assets/Plugins/Android/lifecycle-common-2.2.0.jar
            "2731aa32c5d5440f5be97dfa4b3438db", // Assets/Plugins/Android/lifecycle-livedata-core-2.2.0.aar
            "de28e9beda98241fd8d201f188c06a1c", // Assets/Plugins/Android/lifecycle-runtime-2.2.0.aar
            "f5dd1a91ecb644a7ab9fe169e6fa6896", // Assets/Plugins/Android/lifecycle-viewmodel-2.2.0.aar
            "e3fcfd0ed0e6a4a10bb1e9ca98bb991f", // Assets/Plugins/Android/lifecycle-viewmodel-savedstate-2.2.0.aar
            "ab6be83c5d2584c25922c7433f84c407", // Assets/Plugins/Android/loader-1.0.0.aar
            "d226af9235cbd44e2863e83028c3295f", // Assets/Plugins/Android/localbroadcastmanager-1.0.0.aar
            "d803b05ca4ad24f1cb86af0d24cc5637", // Assets/Plugins/Android/play-services-ads-15.0.1.aar
            "6eae796dea29642f49bafd4ad339bb08", // Assets/Plugins/Android/play-services-ads-19.2.0.aar
            "06b10717493cc476484e2d818d29dd4f", // Assets/Plugins/Android/play-services-ads-9.0.1.aar
            "26c65549003bd47609fa235f11d16bf0", // Assets/Plugins/Android/play-services-ads-base-19.2.0.aar
            "fec7411dadee145f699f9417842b1583", // Assets/Plugins/Android/play-services-ads-identifier-17.0.0.aar
            "542f675713171491aaebd6f87797166b", // Assets/Plugins/Android/play-services-ads-lite-9.0.1.aar
            "53bc481208bdd4d1cbf723399706f7f9", // Assets/Plugins/Android/play-services-base-9.0.1.aar
            "9a0d20c3c9a9941d79b7af35538d7f68", // Assets/Plugins/Android/play-services-basement-10.2.6.aar
            "e1b2095ea7dbf4660b41fc7db5abf750", // Assets/Plugins/Android/play-services-basement-17.0.0.aar
            "1310f1080cf954f48bcc7298b14db053", // Assets/Plugins/Android/play-services-basement-9.0.1.aar
            "af53a1af948e34c17bdd2a6f59cb32ac", // Assets/Plugins/Android/play-services-tasks-9.0.1.aar
            "da95da027f4ed442fba1b2633ed05b3f", // Assets/Plugins/Android/preference-1.1.1.aar
            "2ce41e51ebfe74290bff59a77818dd4d", // Assets/Plugins/Android/savedstate-1.0.0.aar
            "d02bc1a7d63a748faa242a40e7b17ae9", // Assets/Plugins/Android/support-annotations-23.4.0.jar
            "4a7cf91286f474c338652878f852d648", // Assets/Plugins/Android/support-v4-23.4.0.aar
            "39893f518a0e34b86b9972d823277928", // Assets/Plugins/Android/support-vector-drawable-23.4.0.aar
            "2c5db8845a83f4bbca82e2759e76e231", // Assets/Plugins/Android/theoremreach-3.0.0.7.aar
            "07f4302d16bfb42248abcd6387beb164", // Assets/Plugins/Android/theoremreach-3.0.0.9.aar
            "d0ffd31f92ecf461da79fb2a7e0a193e", // Assets/Plugins/Android/theoremreach-3.1.2.aar
            "0c22fed0aebc948ca9ef9765688e639a", // Assets/Plugins/Android/theoremreach-3.1.3.aar
            "d4d29ea3681524cf69ac71ca2664cfed", // Assets/Plugins/Android/theoremreach-3.1.4.aar
            "a98941fa0c4144d7a8fe90ef706788a0", // Assets/Plugins/Android/theoremreach-3.1.5.aar
            "78faed9ac2d73442f92381ebabf6f33d", // Assets/Plugins/Android/theoremreach-3.1.8.aar
            "b2cf7c08aef9f487e916990ed8464bf2", // Assets/Plugins/Android/theoremreach-3.1.9.aar
            "60ed821e914d14968b6d71fd1f82e1a7", // Assets/Plugins/Android/theoremreach-3.2.1.aar
            "3ac183efd1e124512a07208a3aa6b647", // Assets/Plugins/Android/theoremreach-3.2.3.aar
            "36bf423e0684846ba9a746585bd5918e", // Assets/Plugins/Android/theoremreach-3.2.4.aar
            "1d73af44ee08e47c887526f24c5a1603", // Assets/Plugins/Android/theoremreach-3.2.5.aar
            "d4ce45914de614d799b70f4f05d6ac11", // Assets/Plugins/Android/theoremreach-3.2.6.aar
            "3e793feb46b624474bca3006bbbd0bdd", // Assets/Plugins/Android/theoremreach-3.3.1.aar
            "4fce7592c08b449c8b90227c78edbb77", // Assets/Plugins/Android/theoremreach-3.3.3.aar
            "7d8f9a328537d4d17bf3b7281f3555d1", // Assets/Plugins/Android/theoremreach-3.3.5.aar
            "6b4527b2662b14e268350f60b1ba3c9e", // Assets/Plugins/Android/theoremreach-3.4.0.aar
            "4194df363ebb74bcdaf78ebdd91f573b", // Assets/Plugins/Android/theoremreach-3.4.1.aar
            "a8e4c768d18fa4333ac5eedcdd8ff799", // Assets/Plugins/Android/theoremreach-3.4.3.aar
            "6f68aa1844ddc44a1b9477fa78daba13", // Assets/Plugins/Android/theoremreach-3.4.4.aar
            "bf2cddaf6739544e69ad40d45a16679e", // Assets/Plugins/Android/theoremreach-3.4.5.aar
            "78c0689a7aa6d42bfacbf0f1e03e3530", // Assets/Plugins/Android/theoremreach-3.4.6.aar
            "0f52ba9a2ae854bdc9140ca4039bd3ad", // Assets/Plugins/Android/theoremreach-3.5.0.aar
            "824b245cddbf64a10a4fc4685074b0c7", // Assets/Plugins/Android/theoremreach-3.5.1.aar
            "7650d91fa03d84e63a6257b7554691b0", // Assets/Plugins/Android/theoremreach-3.5.2.aar
            "7b0ce9e611c9c432e8a71762cc4eeb6f", // Assets/Plugins/Android/theoremreach-3.5.3.aar
            "be730e25aa07a488baa2a95007e8532b", // Assets/Plugins/Android/theoremreach-3.5.4.aar
            "d8a9a83377244441081a98bd06252558", // Assets/Plugins/Android/theoremreach-3.5.5.aar
            "37a2534f124404e3981a5d8dcd890543", // Assets/Plugins/Android/theoremreach-3.5.6.aar
            "46bd8d4eab20149c09f90fb38ed827cd", // Assets/Plugins/Android/theoremreach-3.5.7.aar
            "b801d8a6a3a5e40f19b73c2941a4799f", // Assets/Plugins/Android/theoremreach-3.6.1.aar
            "689d7a731470a477582c0ad02ac8b174", // Assets/Plugins/Android/theoremreach-3.6.10.aar
            "32d019d50d8794019afe77d3cc733458", // Assets/Plugins/Android/theoremreach-3.6.2.aar
            "7124050cad0854a63bdc55dec3628196", // Assets/Plugins/Android/theoremreach-3.6.5.aar
            "abd185eb333884f7e8b94f69f1f6c8ab", // Assets/Plugins/Android/theoremreach-release.aar
            "07aec4ca8efa14dafac0b9eec6de8909", // Assets/Plugins/Android/vectordrawable-1.1.0.aar
            "2048fe12eb800440f95fc359c399751d", // Assets/Plugins/Android/vectordrawable-animated-1.1.0.aar
            "9e19fea9c374941c2a461b1607c6a667", // Assets/Plugins/TheoremReach.cs
            "af8bde98574a549e3bc372f753591454", // Assets/Plugins/iOS/SDK/TheoremReachSDK.framework
            "bc67332c234124071bfbdf420235e46c", // Assets/Plugins/iOS/SDK/TheoremReachSDK.framework
            "2ba11eecd17d0314f9dc50ff1c526edf", // Assets/Plugins/iOS/SDK/TheoremReachSDK.xcframework
            "c3458ec2e1977429db88ecf3b1bc75f4", // Assets/Plugins/iOS/SDK/TheoremReachSDK.xcframework
            "e6dd295f083dc4a8f9bd6596827de0c9", // Assets/Plugins/iOS/TheoremReachSDK.framework
            "cb491871f2ba24d388e0b2dd12160a3d", // Assets/Plugins/iOS/TheoremReachWrapper.h
            "88e4695d8608b417eb0bb2b6a1d0cf37", // Assets/Plugins/iOS/TheoremReachWrapper.mm
            "efdfcfee87a7a47b69c15a05f7d77800", // Assets/TheoremReach/Editor/TRDependencies.cs
            "f61219db5400e4dbb866557b3a8b8476", // Assets/TheoremReach/Editor/TRPluginInfo.cs
            "7645d9ffc53a549b09497d0a6fe3071d", // Assets/TheoremReach/Editor/TRPostBuildProcessor.cs
            "2431d485dfc3fe14d9e597e20bb1565f", // Assets/TheoremReach/Editor/TheoremReachDependencies.xml
        };

        private static readonly string[] OldFolderGuids =
        {
            "3b2db498a404d44ffbb94c63785d2150", // Assets/Plugins/iOS/SDK
            "b09917be7e659469bb77261d68c264e2", // Assets/TheoremReach
            "c33c5d419f0054b26b6ae9d9b627d5ec", // Assets/TheoremReach/Editor
        };
    }
}
