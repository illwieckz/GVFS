using RGFS.RGFlt;
namespace RGFS.UnitTests.Mock.RGFS.RgFlt
{
    public class MockReliableBackgroundOperations : ReliableBackgroundOperations
    {
        public MockReliableBackgroundOperations()
        {
        }

        public override int Count
        {
            get
            {
                return 0;
            }
        }

        public override void Start()
        {
        }

        public override void Enqueue(RGFltCallbacks.BackgroundGitUpdate backgroundOperation)
        {
        }
    }
}
