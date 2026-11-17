// Assembly-level attributes for test configuration
using NUnit.Framework;

// Force sequential test execution at assembly level
// This ensures tests don't interfere with each other when sharing the same user/session
[assembly: NonParallelizable]
[assembly: LevelOfParallelism(1)]
