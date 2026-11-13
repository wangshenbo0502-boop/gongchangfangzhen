using UnityEditor;

namespace WorkshopDemo.Editor
{
    public static class WorkshopReviewedTankImport
    {
        [MenuItem("Tools/Workshop Demo/Import Reviewed Tank")]
        public static void Import() => WorkshopReviewedCabinetImport.ImportViewerModel(
            "model/TankViewerProject/Assets", "Assets/WorkshopDemo/Art/ReviewedTank",
            "Tank", "silo", "tank", "TankFinish", "TankFinish", "Tank", "tk", 892, 245364);

        public static void ImportAndBuildReview()
        {
            Import();
            WorkshopPlatformBuild.BuildTankReview();
        }
    }
}
