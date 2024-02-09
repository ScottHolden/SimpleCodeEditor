using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace SimpleCodeIndexer
{
    public record CodeFile(
        string Id,
        string FileName,
        string Content,
        int LineStart,
        int LineEnd
    )
    {
        public static CodeFile FromFile(string path)
        {
            string name = Path.GetFileName(path);
            string content = File.ReadAllText(path);
            int lineStart = 0;
            int lineEnd = content.Split('\n').Length; // WOW THIS IS DIRTY!

            return new CodeFile(name, name, content, lineStart, lineEnd);
        }
    }
    public class IndexBuilder(string _aiSearchEndpoint, string _aiSearchKey, string _aiSearchIndex)
    {
        private readonly SearchIndexClient _searchIndexClient = new(
            new Uri(_aiSearchEndpoint),
            new AzureKeyCredential(_aiSearchKey)
        );
        private readonly SearchClient _searchClient = new(
            new Uri(_aiSearchEndpoint),
            _aiSearchIndex,
            new AzureKeyCredential(_aiSearchKey)
        );
        public async Task UpsertAsync(CodeFile item) => await UpsertAsync([item]);
        public async Task UpsertAsync(CodeFile[] items)
        {
            var indexBatch = IndexDocumentsBatch.Upload(items);
            var result = await _searchClient.IndexDocumentsAsync(indexBatch);
        }
        public async Task EnsureExistsAsync()
        {
            string SemanticSearchName = "default";
            string idFieldName = nameof(CodeFile.Id);
            string fileName = nameof(CodeFile.FileName);
            string contentFieldName = nameof(CodeFile.Content);
            string lineStartFieldName = nameof(CodeFile.LineStart);
            string lineEndFieldName = nameof(CodeFile.LineEnd);
            //string embeddingFieldName = nameof(AzureAISearchInsert.embedding);

            var searchIndex = new SearchIndex(_searchClient.IndexName, new SearchField[] {
            new SearchField(idFieldName, SearchFieldDataType.String) {
                IsKey = true,
                IsHidden = false,
                IsFacetable = false,
                IsFilterable = true,
                IsSearchable = false,
                IsSortable = false
            },
            new SearchField(lineStartFieldName, SearchFieldDataType.Int32) {
                IsHidden = false,
                IsFacetable = false,
                IsFilterable = false,
                IsKey = false,
                IsSearchable = false,
                IsSortable = false
            },
            new SearchField(lineEndFieldName, SearchFieldDataType.Int32) {
                IsHidden = false,
                IsFacetable = false,
                IsFilterable = false,
                IsKey = false,
                IsSearchable = false,
                IsSortable = false
            },
            new SearchField(fileName, SearchFieldDataType.String) {
                IsHidden = false,
                IsFacetable = true,
                IsFilterable = true,
                IsKey = false,
                IsSortable = false,
                IsSearchable = true,
                AnalyzerName = LexicalAnalyzerName.EnMicrosoft
            },
            new SearchField(contentFieldName, SearchFieldDataType.String) {
                IsHidden = false,
                IsFacetable = false,
                IsFilterable = false,
                IsKey = false,
                IsSortable = false,
                IsSearchable = true,
                AnalyzerName = LexicalAnalyzerName.EnMicrosoft
            }//,
            //new VectorSearchField(embeddingFieldName, 1536, VectorProfileName)
            })
            {
                /*VectorSearch = new()
                {
                    Algorithms = {
                    new HnswAlgorithmConfiguration(VectorAlgorithmName){
                        Parameters = new()
                        {
                            Metric = VectorSearchAlgorithmMetric.Cosine,
                            M = 4,
                            EfConstruction = 400,
                            EfSearch = 500
                        }
                    }
                },
                    Profiles = {
                    new VectorSearchProfile(VectorProfileName, VectorAlgorithmName)
                }
                },*/
                SemanticSearch = new()
                {
                    DefaultConfigurationName = SemanticSearchName,
                    Configurations = {
                    new SemanticConfiguration(SemanticSearchName, new SemanticPrioritizedFields{
                        TitleField = new SemanticField(fileName),
                        ContentFields = {
                            new SemanticField(contentFieldName)
                        },
                        KeywordsFields = {
                            new SemanticField(fileName)
                        }
                    })
                }
                }
            };

            await _searchIndexClient.CreateOrUpdateIndexAsync(searchIndex, allowIndexDowntime: false);
        }
    }
}
