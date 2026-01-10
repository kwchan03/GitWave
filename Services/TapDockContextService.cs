using Keysight.OpenTap.Wpf;
using OpenTap;

namespace GitWave.Services
{
    public class TapDockContextService
    {
        private ITapDockContext _dockContext;
        private string _currentTestPlanPath;
        private readonly OpenTap.TraceSource _log = Log.CreateSource("GitWave");

        public void Initialize(ITapDockContext dockContext)
        {
            if (dockContext == null)
            {
                _log.Info("[TapPlanContext] ⚠️  ITapDockContext is null");
                return;
            }

            _dockContext = dockContext;
            _log.Info("[TapPlanContext] ✅ Initialized with ITapDockContext");

            // Get current TestPlan path immediately
            TryGetCurrentTestPlanPath();
        }

        public string TryGetCurrentTestPlanPath()
        {
            try
            {
                // ✅ Get TestPlan path directly from ITapDockContext
                var testPlanPath = _dockContext?.Plan?.Path;

                if (!string.IsNullOrEmpty(testPlanPath) && testPlanPath != _currentTestPlanPath)
                {
                    _currentTestPlanPath = testPlanPath;
                    _log.Info($"[TapPlanContext] 📄 TestPlan opened: {_currentTestPlanPath}");
                }

                if (!string.IsNullOrEmpty(testPlanPath))
                {
                    return testPlanPath;
                }
            }
            catch (Exception ex)
            {
                _log.Info($"[TapPlanContext] ❌ Error getting TestPlan path: {ex.Message}");
            }

            return null;
        }
    }
}
