//using GitWave.Models;
//using GitWave.Services;
//using GraphX.Common.Enums;
//using GraphX.Controls;
//using GraphX.Logic.Models;

//namespace GitWave.Logic
//{
//    public class CommitGraphLogic
//    {
//        public GraphArea<CommitVertex, CommitEdge, CommitGraph> GraphArea { get; private set; }

//        public CommitGraphLogic()
//        {
//            GraphArea = new GraphArea<CommitVertex, CommitEdge, CommitGraph>();
//        }

//        public void LoadAndGenerateGraph(string repositoryPath)
//        {
//            // 1. Fetch data using your service
//            List<CommitInfo> commitInfos = GitHistoryReader.FetchCommits(repositoryPath);

//            if (!commitInfos.Any())
//            {
//                GraphArea.ClearLayout();
//                return;
//            }

//            // 2. Convert to QuikGraph structure
//            var graph = BuildGraphFromCommitInfos(commitInfos);

//            // 3. Setup the GraphX Logic Core
//            var logic = new GXLogicCore<CommitVertex, CommitEdge, CommitGraph>(graph);

//            // Layout Algorithm: Sugiyama is suitable for hierarchical DAGs
//            logic.DefaultLayoutAlgorithm = LayoutAlgorithmTypeEnum.Sugiyama;
//            logic.DefaultLayoutAlgorithmParams = logic.AlgorithmFactory.CreateLayoutParameters(LayoutAlgorithmTypeEnum.Sugiyama);
//            ((GraphX.Logic.Algorithms.LayoutAlgorithms.Sugiyama.SugiyamaLayoutParameters)logic.DefaultLayoutAlgorithmParams).Direction = GraphX.Logic.Algorithms.LayoutAlgorithms.Sugiyama.SugiyamaLayoutDirection.TopToBottom;

//            // Edge Routing and Curving for visual clarity
//            logic.DefaultEdgeRoutingAlgorithm = EdgeRoutingAlgorithmTypeEnum.SimpleER;
//            logic.EdgeCurvingEnabled = true;

//            GraphArea.LogicCore = logic;

//            // 4. Generate the visual elements
//            GraphArea.GenerateGraph(true);
//        }

//        private CommitGraph BuildGraphFromCommitInfos(List<CommitInfo> commitInfos)
//        {
//            var graph = new CommitGraph();
//            var verticesBySha = new Dictionary<string, CommitVertex>(StringComparer.Ordinal);

//            // Create all vertices (CommitVertex)
//            foreach (var info in commitInfos)
//            {
//                var vertex = new CommitVertex
//                {
//                    Sha = info.Sha,
//                    MessageShort = info.MessageShort,
//                    AuthorName = info.AuthorName,
//                    Refs = info.Refs,
//                };
//                graph.AddVertex(vertex);
//                verticesBySha.Add(info.Sha, vertex);
//            }

//            // Create all edges (CommitEdge)
//            foreach (var info in commitInfos)
//            {
//                if (verticesBySha.TryGetValue(info.Sha, out var childCommit))
//                {
//                    foreach (var parentSha in info.ParentShas)
//                    {
//                        if (verticesBySha.TryGetValue(parentSha, out var parentCommit))
//                        {
//                            // Edge is from parent (Source) to child (Target) for TopDown layout
//                            graph.AddEdge(new CommitEdge(parentCommit, childCommit));
//                        }
//                    }
//                }
//            }

//            return graph;
//        }
//    }
//}