
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using BrainAtlas.ScriptableObjects;

namespace BrainAtlas.Editor
{
    public static class Pipeline
    {
        public static string DataFolder = "D:\\VBL Common Bulk Files\\BrainAtlas\\Pipelines\\data";

        /// <summary>
        /// (string name, string fullPath)
        /// </summary>
        private static List<(string atlasName, string fullPath)> _atlasInfoList;
        private static AtlasMetaData _atlasMetaData;
        private static AddressableAssetSettings _addressableSettings;

        [MenuItem("BrainAtlas/Run Pipeline")]
        public static void RunPipeline()
        {
            _addressableSettings = AddressableAssetSettingsDefaultObject.Settings;

            _atlasInfoList = new();
            _atlasMetaData = ScriptableObject.CreateInstance<AtlasMetaData>();

            Debug.Log("(BrainAtlas) Running pipeline...");
            GetAtlasList();

            // Save the atlas metadata object
            string atlasMetaPath = Path.Join("Assets/AddressableAssets", "metadataSO.asset");
            CreateAddressablesHelper(_atlasMetaData, atlasMetaPath, _addressableSettings.DefaultGroup);

            for (int i = 0; i < _atlasInfoList.Count; i++) {
                Debug.Log(_atlasInfoList[i].atlasName);
            }

            // int[] skip = {0, 1, 2, 3, 5, 7, 10, 11};
            int[] skip = {};

            for (int i = 0; i < _atlasInfoList.Count; i++)
            {
                if (skip.Contains(i))
                    continue;

                var atlasInfo = _atlasInfoList[i];

                Debug.Log($"(Pipeline) Running for {atlasInfo.atlasName}");
                var updatedAtlasInfo = SetupAddressables(atlasInfo);

                Debug.Log($"(Pipeline) Building SOs for {atlasInfo.atlasName}");
                ////Build the Atlas ScriptableObjects
                AtlasMeta2SO(updatedAtlasInfo);

                Debug.Log($"(Pipeline) Converting mesh files into prefabs for {atlasInfo.atlasName}");
                ////Convert mesh files 2 prefabs
                MeshFiles2Prefabs(updatedAtlasInfo);

                Debug.Log($"(Pipeline) Building textures for {atlasInfo.atlasName}");
                AnnotationReference2Textures(updatedAtlasInfo);
            }

            Debug.Log($"(Pipeline) Cleaning up...");
            EditorUtility.SetDirty(_addressableSettings);
            AssetDatabase.SaveAssets();
            Debug.Log($"(Pipeline) Complete");
        }

        public static void GetAtlasList()
        {
            string[] subdirectories = Directory.GetDirectories(DataFolder);

            _atlasMetaData.AtlasNames = new();

            foreach (string atlasPath in subdirectories)
            {
                string atlasName = Path.GetFileName(atlasPath);
                _atlasInfoList.Add((atlasName, atlasPath));

                _atlasMetaData.AtlasNames.Add(atlasName);
            }
        }

        public static (string atlasName, string atlasPath, AddressableAssetGroup assetGroup) SetupAddressables((string atlasName, string atlasPath) atlasInfo)
        {
            string path = Path.Join("Assets/AddressableAssets", atlasInfo.atlasName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            // Create a new group
            AddressableAssetGroup atlasGroup = (_addressableSettings.groups.Any(x => x.Name == atlasInfo.atlasName)) ?
                _addressableSettings.groups.Find(x => x.Name == atlasInfo.atlasName) :
                _addressableSettings.CreateGroup(atlasInfo.atlasName, false, false, true, null);

            // Set the bundle mode to pack separately
            BundledAssetGroupSchema schema = atlasGroup.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                schema = atlasGroup.AddSchema<BundledAssetGroupSchema>();
            }
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            schema.BuildPath.SetVariableByName(_addressableSettings,
                        _addressableSettings.profileSettings.GetValueById("Default", "RemoteBuildPath"));
            schema.LoadPath.SetVariableByName(_addressableSettings,
                        _addressableSettings.profileSettings.GetValueById("Default", "RemoteLoadPath"));
            schema.InternalIdNamingMode = BundledAssetGroupSchema.AssetNamingMode.GUID;

            return (atlasInfo.atlasName, atlasInfo.atlasPath, atlasGroup);
        }


        /// <summary>
        /// Convert the metadata, structure tree, and mesh center coordinates to an Atlas scriptable object
        /// </summary>
        public static void AtlasMeta2SO((string atlasName, string atlasPath, AddressableAssetGroup assetGroup) atlasInfo)
        {
            // Create a new ReferenceAtlasData SO
            ReferenceAtlasData atlasData = ScriptableObject.CreateInstance<ReferenceAtlasData>();

            // Load the metadata json file into a string
            string metadataFile = Path.Join(atlasInfo.atlasPath, "meta.json");
            string metaStr = File.ReadAllText(metadataFile);

            // Deserialize the JSON string into a C# object
            MetaJSON data = JsonUtility.FromJson<MetaJSON>(metaStr);

            atlasData.name = data.name;
            // note that we invet dimensions to get ap/ml/dv instead of a/s/r
            Vector3 dimensionsIdx = new Vector3(data.shape[0], data.shape[2], data.shape[1]);
            Vector3 resolution = new Vector3(data.resolution[0], data.resolution[2], data.resolution[1]);
            // to get full dimensions we need the full resolution
            atlasData.Dimensions = Vector3.Scale(dimensionsIdx, resolution) / 1000f;
            Debug.Log(atlasData.Dimensions);
            atlasData.Resolution = resolution;

            // DEFAULT AREAS
            switch (atlasInfo.atlasName)
            {
                case "allen_mouse_25um":
                    atlasData.DefaultAreas = new int[] { 184, 500, 453, 1057, 677, 247, 669, 31, 972, 44, 714, 95, 254, 22, 541, 922, 698, 895, 1089, 703, 623, 343, 512 };
                    break;
                case "allen_mouse_50um":
                    atlasData.DefaultAreas = new int[] { 184, 500, 453, 1057, 677, 247, 669, 31, 972, 44, 714, 95, 254, 22, 541, 922, 698, 895, 1089, 703, 623, 343, 512 };
                    break;
                case "allen_mouse_100um":
                    atlasData.DefaultAreas = new int[] { 184, 500, 453, 1057, 677, 247, 669, 31, 972, 44, 714, 95, 254, 22, 541, 922, 698, 895, 1089, 703, 623, 343, 512 };
                    break;
                case "whs_sd_rat_39um":
                    atlasData.DefaultAreas = new int[] { 1034, 1096, 1084, 1038, 1081, 1097, 1048, 1057, 1061, 1055, 1059, 1069, 1056, 1065, 1072, 1020, 1047, 58, 1044, 74, 1046, 56, 1045, 75, 1043 };
                    break;
                case "whs_sd_rat_78um":
                    atlasData.DefaultAreas = new int[] { 1034, 1096, 1084, 1038, 1081, 1097, 1048, 1057, 1061, 1055, 1059, 1069, 1056, 1065, 1072, 1020, 1047, 58, 1044, 74, 1046, 56, 1045, 75, 1043 };
                    break;
                case "princeton_mouse_20um":
                    atlasData.DefaultAreas = new int[] { 184, 500, 453, 1057, 677, 247, 669, 31, 972, 44, 714, 95, 254, 22, 541, 922, 698, 895, 1089, 703, 623, 343, 512 };
                    break;
                case "allen_human_500um":
                    atlasData.DefaultAreas = new int[] { 10467 , 10465 , 10390, 10331, 10159 ,
                    12113, 12176, 12155, 12148, 12131, 12139, 12179,
                    10595, 10557, 10654, 10669, 10668, 10649, 10651, 10650};
                    break;
                case "azba_zfish_8um":
                    atlasData.DefaultAreas = new int[] { 9999 };
                    break;
                case "prairie_vole_25um":
                    atlasData.DefaultAreas = new int[] { 343, 512, 623, 688 };
                    break;
                case "sju_cavefish_2um":
                    atlasData.DefaultAreas = new int[] {
                        8, 21, 26, 7, 17, 24, 9, 2, 5, 10,
                        13, 1, 11, 3, 4, 19, 6, 15, 14, 16,
                        18, 25, 22, 23, 27, 20
                    };
                    break;
                case "unam_axolotl_40um":
                    atlasData.DefaultAreas = new int[] { 105, 106, 104, 102, 101, 103 };
                    break;
                case "allen_mouse_bluebrain_barrels_25um":
                    atlasData.DefaultAreas = new int[] { 184, 500, 453, 1057, 677, 247, 669, 31, 972, 44, 714, 95, 254, 22, 541, 922, 698, 895, 1089, 703, 623, 343, 512 };
                    break;
                default:
                    Debug.LogWarning($"No default areas for atlas {atlasInfo.atlasName}");
                    break;
            }

            // STRUCTURE ONTOLOGY
            // Load the ontology file
            string structuresFile = Path.Join(atlasInfo.atlasPath, "structures.json");
            string structuresListStr = File.ReadAllText(structuresFile);
            // wrap the structure list so that it can be parsed properly
            string wrappedStructuresListStr = $"{{\"structures\":{structuresListStr}}}";

            // Two step process to obtain the JSON out of this
            StructureListJSON structuresList = JsonUtility.FromJson<StructureListJSON>(wrappedStructuresListStr);
            // Now parse the structures
            List<OntologyTuple> ontologyData = new();

                //int, (string, string, Color, int[])> ontologyData = new();

            foreach (var structure in structuresList.structures)
            {
                // figure out the remapping

                int remap_no_layers;
                if (structure.name.ToLower().Contains("layer"))
                    remap_no_layers = structure.structure_id_path[^2];
                else
                    remap_no_layers = structure.id;

                int remap_defaults;
                remap_defaults = structure.structure_id_path.Reverse().FirstOrDefault(x => atlasData.DefaultAreas.Contains(x));

                if (remap_defaults == 0)
                    remap_defaults = structure.id;

                ontologyData.Add(new OntologyTuple(structure.id,
                    structure.acronym,
                    structure.name,
                    new Color(structure.rgb_triplet[0] / 255f, structure.rgb_triplet[1] / 255f, structure.rgb_triplet[2] / 255f),
                    structure.structure_id_path,
                    remap_no_layers,
                    remap_defaults));
            }

            // Initialize ontology
            atlasData._privateOntologyData = ontologyData;

            // Create a temporary Ontology for local use
            Material tempMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/NullMaterial");
            Ontology tempOntology = new Ontology(ontologyData, null, tempMat);

            // MESH CENTERS

            // Load the mesh centers file
            string meshCentersFile = Path.Join(atlasInfo.atlasPath, "mesh_centers.csv");
            string meshCentersStr = File.ReadAllText(meshCentersFile);

            var meshCentersCSV = CSVReader.ParseText(meshCentersStr);
            List<IV3Tuple> meshCentersum = new();

            for (int i = 0; i < meshCentersCSV.Count; i++)
            {
                var row = meshCentersCSV[i];
                string acronym = (string)row["structure_name"];
                Vector3 center = new Vector3(
                    (float)row["ap"] / 1000f,
                    (float)row["ml"] / 1000f,
                    (float)row["dv"] / 1000f);
                Vector3 center_lh = new Vector3(
                    (float)row["ap-lh"] / 1000f,
                    (float)row["ml-lh"] / 1000f,
                    (float)row["dv-lh"] / 1000f);
                meshCentersum.Add(new IV3Tuple(tempOntology.Acronym2ID(acronym), center, center_lh));
            }

            atlasData._privateMeshCenters = meshCentersum;

            // Save the atlas data into the Addressables folder
            //atlasData
            string atlasSOPath = Path.Join("Assets/AddressableAssets", atlasInfo.atlasName, $"{atlasInfo.atlasName}.asset");
            CreateAddressablesHelper(atlasData, atlasSOPath, atlasInfo.assetGroup);
        }

        public static void MeshFiles2Prefabs((string atlasName, string atlasPath, AddressableAssetGroup atlasGroup) atlasInfo)
        {
            // MESH FILES 2 ADDRESSABLES
            string targetFolderPath = Path.Join("Assets/ImportedObjFiles", atlasInfo.atlasName);
            if (!Directory.Exists(targetFolderPath))
                Directory.CreateDirectory(targetFolderPath);
            string meshFolderPath = Path.Join("Assets/AddressableAssets/", atlasInfo.atlasName, "meshes");
            if (!Directory.Exists(meshFolderPath))
                Directory.CreateDirectory(meshFolderPath);
            string atlasFolderPath = Path.Join("Assets/AddressableAssets/", atlasInfo.atlasName);

            // get the metadata for this atlas
            var atlasData = AssetDatabase.LoadAssetAtPath<ReferenceAtlasData>(Path.Join("Assets/AddressableAssets", atlasInfo.atlasName, $"{atlasInfo.atlasName}.asset"));

            // get the contents of the mesh directory, copy them all over into addressables
            Debug.Log(atlasInfo.atlasPath);
            string meshPath = $"{atlasInfo.atlasPath}/meshes/";
            string[] meshFiles = Directory.GetFiles(meshPath, "*.obj");

            // Create the parent prefab
            GameObject parentGO = new GameObject();
            parentGO.name = $"{atlasInfo.atlasName}Parent";
            parentGO.transform.rotation = Quaternion.Euler(new Vector3(0, 90, 180));
            parentGO.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            parentGO.transform.localPosition = new Vector3(-atlasData.Dimensions.y / 2f, atlasData.Dimensions.z / 2f, atlasData.Dimensions.x / 2f);
            string parentPrefabPath = Path.Join(atlasFolderPath, $"{parentGO.name}.prefab");
            PrefabUtility.SaveAsPrefabAsset(parentGO, parentPrefabPath);
            MarkAssetAddressable(parentPrefabPath, atlasInfo.atlasGroup);

            GameObject.DestroyImmediate(parentGO);

            // Copy all files
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < meshFiles.Length; i++)
            {
                string meshFile = meshFiles[i];
                string name = Path.GetFileName(meshFile);

                string targetFilePath = Path.Join(targetFolderPath, name);

                Debug.Log((meshFile, targetFilePath));

                File.Copy(meshFile, targetFilePath, true);
                AssetDatabase.ImportAsset(targetFilePath);
            }
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();

            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < meshFiles.Length; i++)
            {
                string meshFile = meshFiles[i];
                string name = Path.GetFileName(meshFile);

                string targetFilePath = Path.Join(targetFolderPath, name);

                ModelImporter modelImporter = AssetImporter.GetAtPath(targetFilePath) as ModelImporter;
                modelImporter.isReadable = true;
                modelImporter.importNormals = ModelImporterNormals.Calculate;
                modelImporter.SaveAndReimport();
            }
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();

            for (int i = 0; i < meshFiles.Length; i++)
            {
                string meshFile = meshFiles[i];

                string name = Path.GetFileName(meshFile);
                string targetFilePath = Path.Join(targetFolderPath, name);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(targetFilePath);

                string goName = Path.GetFileNameWithoutExtension(meshFile);

                // The object right now has a parent gameobject and then a child, let's turn it into a single gameobject
                GameObject objModelPrefab = new GameObject(goName);
                MeshFilter meshFilter = objModelPrefab.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                Renderer rend = objModelPrefab.AddComponent<MeshRenderer>();
                rend.receiveShadows = false;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                string prefabPath = Path.Join(meshFolderPath, $"{Path.GetFileNameWithoutExtension(targetFilePath)}.prefab");
                PrefabUtility.SaveAsPrefabAsset(objModelPrefab, prefabPath);

                MarkAssetAddressable(prefabPath, atlasInfo.atlasGroup);

                if (name.Contains('L'))
                {
                    // If this is L model, let's build the right model now
                    string nameR = $"{name.Substring(0, goName.Length - 1)}R";
                    GameObject objModelPrefabR = new GameObject(nameR);
                    objModelPrefabR.transform.localScale = new Vector3(1f, 1f, -1f);
                    objModelPrefabR.transform.localPosition = new Vector3(0f, 0f, atlasData.Dimensions.y * 1000f);
                    MeshFilter meshFilterR = objModelPrefabR.AddComponent<MeshFilter>();
                    meshFilterR.mesh = mesh;
                    Renderer rendR = objModelPrefabR.AddComponent<MeshRenderer>();
                    rendR.receiveShadows = false;
                    rendR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    string prefabPathR = Path.Join(meshFolderPath, $"{nameR}.prefab");
                    PrefabUtility.SaveAsPrefabAsset(objModelPrefabR, prefabPathR);

                    MarkAssetAddressable(prefabPathR, atlasInfo.atlasGroup);
                    GameObject.DestroyImmediate(objModelPrefabR);
                }

                GameObject.DestroyImmediate(objModelPrefab);
            }
        }

        /// <summary>
        /// Build the annotation color texture, and reference texture
        /// </summary>
        public static void AnnotationReference2Textures((string atlasName, string atlasPath, AddressableAssetGroup atlasGroup) atlasInfo)
        {
            Debug.Log($"(Pipeline:{atlasInfo.atlasName}) Starting annotation file to reference texture conversion.");
            //// get the metadata for this atlas
            var atlasData = AssetDatabase.LoadAssetAtPath<ReferenceAtlasData>(Path.Join("Assets/AddressableAssets", atlasInfo.atlasName, $"{atlasInfo.atlasName}.asset"));
            var ontologyData = atlasData._privateOntologyData;
            Dictionary<int, (string acronym, string name, Color color, int[] path)> ontologyDataDict = new();
            foreach (var data in ontologyData)
                ontologyDataDict.Add(data.id, (data.acronym, data.name, data.color, data.structure_id_path));

            int apLength = Mathf.RoundToInt(atlasData.Dimensions.x / atlasData.Resolution.x * 1000f);
            int mlWidth = Mathf.RoundToInt(atlasData.Dimensions.y / atlasData.Resolution.y * 1000f);
            int dvDepth = Mathf.RoundToInt(atlasData.Dimensions.z / atlasData.Resolution.z * 1000f);

            // Load the bytes file
            byte[] annotationBytes = File.ReadAllBytes(Path.Join(atlasInfo.atlasPath, "annotation.bytes"));

            uint[] uannotationData = new uint[annotationBytes.Length / 4];
            Buffer.BlockCopy(annotationBytes, 0, uannotationData, 0, annotationBytes.Length);

            // Convert to int[]
            int[] annotationData = new int[uannotationData.Length];
            for (int i = 0; i < uannotationData.Length; i++)
                annotationData[i] = (int)uannotationData[i];

            Debug.Log($"(Pipeline:{atlasInfo.atlasName}) Annotations convered to int, flattening array.");

            // Also save the annotation data itself
            AnnotationData annotationDataSO = ScriptableObject.CreateInstance<AnnotationData>();

            // re-organize the data to be in ap/ml/dv order
            int[] reorderedArray = new int[annotationData.Length];

            for (int i = 0; i < apLength; i++)
            {
                for (int j = 0; j < dvDepth; j++)
                {
                    for (int k = 0; k < mlWidth; k++)
                    {
                        int oldIndex = i * dvDepth * mlWidth + j * mlWidth + k;
                        int newIndex = i * mlWidth * dvDepth + k * dvDepth + j;
                        reorderedArray[newIndex] = annotationData[oldIndex];
                    }
                }
            }

            Debug.Log($"(Pipeline:{atlasInfo.atlasName}) Building annotation SO and addressables prefab.");
            annotationDataSO.Annotations = reorderedArray;

            string annotationDataSOPath = $"Assets/AddressableAssets/{atlasInfo.atlasName}/annotations.asset";
            CreateAddressablesHelper(annotationDataSO, annotationDataSOPath, atlasInfo.atlasGroup);


            Debug.Log($"(Pipeline:{atlasInfo.atlasName}) Annotations complete. Moving to atlas and reference textures.");

            //// Create the texture

            Texture3D atlasTexture = ConvertArrayToTexture(annotationData, apLength, mlWidth, dvDepth, ontologyDataDict);

            // Save to an asset file
            string atlasTexturePath = $"Assets/AddressableAssets/{atlasInfo.atlasName}/annotationTexture.asset";

            if (File.Exists(atlasTexturePath))
                AssetDatabase.DeleteAsset(atlasTexturePath);
            AssetDatabase.CreateAsset(atlasTexture, atlasTexturePath);
            _addressableSettings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(atlasTexturePath), atlasInfo.atlasGroup);

            //CreateAddressablesHelper(atlasTexture, atlasTexturePath, atlasInfo.atlasGroup);

            Debug.Log($"(Pipeline:{atlasInfo.atlasName}) Annotation texture built. Moving to reference texture.");

            // Deal with the texture data
            byte[] referenceBytes = File.ReadAllBytes(Path.Join(atlasInfo.atlasPath, "reference.bytes"));

            float[] ureferenceData = new float[referenceBytes.Length / 4];
            Buffer.BlockCopy(referenceBytes, 0, ureferenceData, 0, referenceBytes.Length);

            Texture3D referenceTexture = ConvertArrayToTexture(ureferenceData, apLength, mlWidth, dvDepth);

            string refTexturePath = $"Assets/AddressableAssets/{atlasInfo.atlasName}/referenceTexture.asset";
            if (File.Exists(refTexturePath))
                AssetDatabase.DeleteAsset(refTexturePath);
            AssetDatabase.CreateAsset(referenceTexture, refTexturePath);
            _addressableSettings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(refTexturePath), atlasInfo.atlasGroup);

            //CreateAddressablesHelper(referenceTexture, refTexturePath, atlasInfo.atlasGroup);
            Debug.Log($"(Pipeline:{atlasInfo.atlasName}) Reference texture built.");
        }

        #region private helpers
        private static void CreateAddressablesHelper(UnityEngine.Object assetData, string assetPath, AddressableAssetGroup assetGroup)
        {
            if (File.Exists(assetPath))
                AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(assetData, assetPath);

            MarkAssetAddressable(assetPath, assetGroup);
        }

        private static void MarkAssetAddressable(string prefabPath, AddressableAssetGroup assetGroup)
        {
            _addressableSettings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(prefabPath), assetGroup);
        }

        private static Texture3D ConvertArrayToTexture(int[] data, int apLength, int mlWidth, int dvDepth,
            Dictionary<int, (string acronym, string name, Color color, int[] path)> ontologyDict)
        {
            Debug.Log($"(Pipeline: ConvertArray2Tex) Creating an array of size ({apLength},{mlWidth},{dvDepth}), this could take a lot of memory!");

            Texture3D atlasTexture = new Texture3D(apLength, mlWidth, dvDepth, TextureFormat.RGBA32, false);
            //Texture3D atlasTexture = new Texture3D(apLength, mlWidth, dvDepth, TextureFormat.BC7, false);
            atlasTexture.filterMode = FilterMode.Point;
            atlasTexture.wrapMode = TextureWrapMode.Clamp;

            Color transparentBlack = new Color(0f, 0f, 0f, 0f);

            //Color[] colorData = new Color[data.Length];
            //for (int i = 0; i < data.Length; i++)
            //{
            //    int atlasID = data[i];

            //    // but here we're back to ap/ml/dv because that's how we'll store it locally
            //    if (atlasID <= 0)
            //        colorData[i] = transparentBlack;
            //    else if (ontologyDict.TryGetValue(atlasID, out var value))
            //        colorData[i] = value.color;
            //    else
            //        colorData[i] = Color.black;
            //}

            int idx = 0;
            // Note that the volume in the python pipeline is in ap/dv/ml order, so the loop here matches that
            for (int ap = 0; ap < apLength; ap++)
                for (int dv = 0; dv < dvDepth; dv++)
                    for (int ml = 0; ml < mlWidth; ml++)
                    {
                        int atlasID = data[idx++];

                        // but here we're back to ap/ml/dv because that's how we'll store it locally
                        if (atlasID <= 0)
                            atlasTexture.SetPixel(ap, ml, dv, transparentBlack);
                        else if (ontologyDict.TryGetValue(atlasID, out var value))
                            atlasTexture.SetPixel(ap, ml, dv, value.color);
                        else
                            atlasTexture.SetPixel(ap, ml, dv, Color.black);
                    }

            atlasTexture.Apply();

            return atlasTexture;
        }

        private static Texture3D ConvertArrayToTexture(float[] data, int apLength, int mlWidth, int dvDepth)
        {
            Debug.Log($"(Pipeline: ConvertArray2Tex) Creating an array of size ({apLength},{mlWidth},{dvDepth}), this could take a lot of memory!");

            Texture3D atlasTexture = new Texture3D(apLength, mlWidth, dvDepth, TextureFormat.RGB24, false);
            //Texture3D atlasTexture = new Texture3D(apLength, mlWidth, dvDepth, TextureFormat.BC4, false);
            atlasTexture.filterMode = FilterMode.Point;
            atlasTexture.wrapMode = TextureWrapMode.Clamp;

            int idx = 0;
            // Note that the volume in the python pipeline is in ap/dv/ml order, so the loop here matches that
            for (int ap = 0; ap < apLength; ap++)
                for (int dv = 0; dv < dvDepth; dv++)
                    for (int ml = 0; ml < mlWidth; ml++)
                    {
                        // but here we're back to ap/ml/dv because that's how we'll store it locally
                        float val = data[idx++];
                        atlasTexture.SetPixel(ap, ml, dv, new Color(val, val, val));
                    }

            atlasTexture.Apply();

            return atlasTexture;
        }
        #endregion
    }

    [Serializable]
    public class MetaJSON
    {
        public string name;
        public string citation;
        public string atlas_link;
        public string species;
        public bool symmetric;
        public float[] resolution;
        public string orientation;
        public string version;
        public float[] shape;
        //public List<List<float>> transform_to_bg; // we have to ignore this because we can't parse a list<vec4>
        public string[] additional_references;
    }

    [Serializable]
    public class StructureListJSON
    {
        public StructureJSON[] structures;
        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }

    [Serializable]
    public class StructureJSON
    {
        public string acronym;
        public int id;
        public string name;
        public int[] structure_id_path;
        public int[] rgb_triplet;

        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
#endif