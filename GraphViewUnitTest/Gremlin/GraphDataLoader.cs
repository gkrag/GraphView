﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Collections.ObjectModel;
using GraphView;
using Microsoft.Azure.Documents.Linq;

namespace GraphViewUnitTest.Gremlin
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods to generate the various sample TinkerPop graphs.
    /// </summary>
    public static class GraphDataLoader
    {
        public static void ResetToCompatibleData_Modern(string endpoint, string authKey, string databaseId, string collectionId, bool useReverseEdge)
        {
            DocumentClient client = new DocumentClient(
                new Uri(endpoint),
                authKey,
                new ConnectionPolicy {
                    ConnectionMode = ConnectionMode.Direct,
                    ConnectionProtocol = Protocol.Tcp
                });

            // Remove all existing documents
            //JObject[] docs = client.CreateDocumentQuery<JObject>(
            //    collection.SelfLink,
            //    new FeedOptions {
            //        EnableCrossPartitionQuery = true
            //    }).ToArray();
            //Task.WaitAll(
            //    docs.Select(doc => client.DeleteDocumentAsync(
            //        UriFactory.CreateDocumentUri(connection.DocDBDatabaseId, connection.DocDBCollectionId, (string)doc["id"]),
            //        new RequestOptions {
            //            PartitionKey = new PartitionKey(connection.GetDocumentPartition(doc))
            //        }
            //        )).ToArray());

            // Just remove & recreate the collection
            try {
                client.DeleteDocumentCollectionAsync(
                    UriFactory.CreateDocumentCollectionUri(databaseId, collectionId)
                ).Wait();
            }
            catch (AggregateException aggex) {
                aggex.Handle(ex => ((ex as DocumentClientException)?.Error.Code == "NotFound"));
            }

            client.CreateDocumentCollectionAsync(
                UriFactory.CreateDatabaseUri(databaseId),
                new DocumentCollection {
                    Id = collectionId,
                    PartitionKey = new PartitionKeyDefinition {
                        Paths = new Collection<string> { "/label" }
                    }
                }
            ).Wait();
            DocumentCollection collection = client.ReadDocumentCollectionAsync(
                UriFactory.CreateDocumentCollectionUri(databaseId, collectionId)).Result;


            // Add new documents
            List<Task> tasks = new List<Task>();
            Action<string> createDoc = (docString) => {
                tasks.Add(client.CreateDocumentAsync(collection.SelfLink, JObject.Parse(docString), disableAutomaticIdGeneration: true));
            };
            createDoc("{\"label\":\"person\",\"id\":\"dummy\",\"name\":\"marko\",\"age\":29}");
            createDoc("{\"label\":\"person\",\"id\":\"特殊符号\",\"name\":\"vadas\",\"age\":27}");
            createDoc("{\"label\":\"software\",\"id\":\"这是一个中文ID\",\"name\":\"lop\",\"lang\":\"java\"}");
            createDoc("{\"label\":\"person\",\"id\":\"引号\",\"name\":\"josh\",\"age\":32}");
            createDoc("{\"label\":\"software\",\"id\":\"中文English\",\"name\":\"ripple\",\"lang\":\"java\"}");
            createDoc("{\"label\":\"person\",\"name\":\"peter\",\"age\":35,\"id\":\"ID_13\"}");

            if (useReverseEdge) {
                createDoc("{\"id\":\"ID_15\",\"_is_reverse\":false,\"_vertex_id\":\"dummy\",\"_edge\":[{\"label\":\"knows\",\"weight\":0.5,\"id\":\"ID_14\",\"_sinkV\":\"特殊符号\",\"_sinkVLabel\":\"person\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_16\",\"_is_reverse\":true,\"_vertex_id\":\"特殊符号\",\"_edge\":[{\"label\":\"knows\",\"weight\":0.5,\"id\":\"ID_14\",\"_srcV\":\"dummy\",\"_srcVLabel\":\"person\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_18\",\"_is_reverse\":false,\"_vertex_id\":\"dummy\",\"_edge\":[{\"label\":\"knows\",\"weight\":1,\"id\":\"ID_17\",\"_sinkV\":\"引号\",\"_sinkVLabel\":\"person\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_19\",\"_is_reverse\":true,\"_vertex_id\":\"引号\",\"_edge\":[{\"label\":\"knows\",\"weight\":1,\"id\":\"ID_17\",\"_srcV\":\"dummy\",\"_srcVLabel\":\"person\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_21\",\"_is_reverse\":false,\"_vertex_id\":\"dummy\",\"_edge\":[{\"label\":\"created\",\"weight\":0.4,\"id\":\"ID_20\",\"_sinkV\":\"这是一个中文ID\",\"_sinkVLabel\":\"software\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_22\",\"_is_reverse\":true,\"_vertex_id\":\"这是一个中文ID\",\"_edge\":[{\"label\":\"created\",\"weight\":0.4,\"id\":\"ID_20\",\"_srcV\":\"dummy\",\"_srcVLabel\":\"person\"}],\"label\":\"software\"}");
                createDoc("{\"id\":\"ID_24\",\"_is_reverse\":false,\"_vertex_id\":\"引号\",\"_edge\":[{\"label\":\"created\",\"weight\":1,\"id\":\"ID_23\",\"_sinkV\":\"中文English\",\"_sinkVLabel\":\"software\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_25\",\"_is_reverse\":true,\"_vertex_id\":\"中文English\",\"_edge\":[{\"label\":\"created\",\"weight\":1,\"id\":\"ID_23\",\"_srcV\":\"引号\",\"_srcVLabel\":\"person\"}],\"label\":\"software\"}");
                createDoc("{\"id\":\"ID_27\",\"_is_reverse\":false,\"_vertex_id\":\"引号\",\"_edge\":[{\"label\":\"created\",\"weight\":0.4,\"id\":\"ID_26\",\"_sinkV\":\"这是一个中文ID\",\"_sinkVLabel\":\"software\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_28\",\"_is_reverse\":true,\"_vertex_id\":\"这是一个中文ID\",\"_edge\":[{\"label\":\"created\",\"weight\":0.4,\"id\":\"ID_26\",\"_srcV\":\"引号\",\"_srcVLabel\":\"person\"}],\"label\":\"software\"}");
                createDoc("{\"id\":\"ID_30\",\"_is_reverse\":false,\"_vertex_id\":\"ID_13\",\"_edge\":[{\"label\":\"created\",\"weight\":0.2,\"id\":\"ID_29\",\"_sinkV\":\"这是一个中文ID\",\"_sinkVLabel\":\"software\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_31\",\"_is_reverse\":true,\"_vertex_id\":\"这是一个中文ID\",\"_edge\":[{\"label\":\"created\",\"weight\":0.2,\"id\":\"ID_29\",\"_srcV\":\"ID_13\",\"_srcVLabel\":\"person\"}],\"label\":\"software\"}");
            }
            else {
                createDoc("{\"id\":\"ID_15\",\"_is_reverse\":false,\"_vertex_id\":\"dummy\",\"label\":\"person\",\"_edge\":[{\"label\":\"knows\",\"weight\":0.5,\"id\":\"ID_14\",\"_sinkV\":\"特殊符号\",\"_sinkVLabel\":\"person\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_17\",\"_is_reverse\":false,\"_vertex_id\":\"dummy\",\"label\":\"person\",\"_edge\":[{\"label\":\"knows\",\"weight\":1,\"id\":\"ID_16\",\"_sinkV\":\"引号\",\"_sinkVLabel\":\"person\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_19\",\"_is_reverse\":false,\"_vertex_id\":\"dummy\",\"label\":\"person\",\"_edge\":[{\"label\":\"created\",\"weight\":0.4,\"id\":\"ID_18\",\"_sinkV\":\"这是一个中文ID\",\"_sinkVLabel\":\"software\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_21\",\"_is_reverse\":false,\"_vertex_id\":\"引号\" ,\"label\":\"person\",\"_edge\":[{\"label\":\"created\",\"weight\":1,\"id\":\"ID_20\",\"_sinkV\":\"中文English\",\"_sinkVLabel\":\"software\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_23\",\"_is_reverse\":false,\"_vertex_id\":\"引号\" ,\"label\":\"person\",\"_edge\":[{\"label\":\"created\",\"weight\":0.4,\"id\":\"ID_22\",\"_sinkV\":\"这是一个中文ID\",\"_sinkVLabel\":\"software\"}],\"label\":\"person\"}");
                createDoc("{\"id\":\"ID_25\",\"_is_reverse\":false,\"_vertex_id\":\"ID_13\",\"label\":\"person\",\"_edge\":[{\"label\":\"created\",\"weight\":0.2,\"id\":\"ID_24\",\"_sinkV\":\"这是一个中文ID\",\"_sinkVLabel\":\"software\"}],\"label\":\"person\"}");
            }

            // Wait for all of them to finish
            Task.WaitAll(tasks.ToArray());

            client.Dispose();
        }


        /// <summary>
        /// Generates and Loads the correct Graph Db, on the local document Db instance.
        /// </summary>
        /// <param name="graphData">The type of graph data to load from among the TinkerPop samples.</param>
        /// <param name="useReverseEdge"></param>
        public static void LoadGraphData(GraphData graphData)
        {
            switch (graphData)
            {
                case GraphData.CLASSIC:
                    LoadClassicGraphData();
                    break;
                case GraphData.MODERN:
                    LoadModernGraphData();
                    break;
                case GraphData.CREW:
                    throw new NotImplementedException("Crew requires supporting properties as documents themselves! This implementation currently does not support that functionality!!!");
                case GraphData.GRATEFUL:
                    throw new NotImplementedException("I'm not a fan of The Grateful Dead!");
                default:
                    throw new NotImplementedException("No idea how I ended up here!");
            }
        }

        /// <summary>
        /// Clears the Correct Graph on the local document Db instance, by clearing the appropriate collection.
        /// </summary>
        /// <param name="graphData">The type of graph data to clear from among the TinkerPop samples.</param>
        public static void ClearGraphData(GraphData graphData)
        {
            switch (graphData)
            {
                case GraphData.CLASSIC:
                    ClearGraphData(ConfigurationManager.AppSettings["DocDBCollectionClassic"]);
                    break;
                case GraphData.MODERN:
                    ClearGraphData(ConfigurationManager.AppSettings["DocDBCollectionModern"]);
                    break;
                case GraphData.CREW:
                    throw new NotImplementedException("Crew requires supporting properties as documents themselves! This implementation currently does not support that functionality!!!");
                case GraphData.GRATEFUL:
                    throw new NotImplementedException("I'm not a fan of The Grateful Dead!");
                default:
                    throw new NotImplementedException("No idea how I ended up here!");
            }
        }

        private static void LoadClassicGraphData()
        {
            //string endpoint = ConfigurationManager.AppSettings["DocDBEndPoint"];
            string endpoint = ConfigurationManager.AppSettings["DocDBEndPointLocal"];
            //string authKey = ConfigurationManager.AppSettings["DocDBKey"];
            string authKey = ConfigurationManager.AppSettings["DocDBKeyLocal"];
            string databaseId = ConfigurationManager.AppSettings["DocDBDatabaseGremlin"];
            string collectionId = ConfigurationManager.AppSettings["DocDBCollectionClassic"];

            GraphViewConnection connection = GraphViewConnection.ResetGraphAPICollection(endpoint, authKey, databaseId, collectionId, AbstractGremlinTest.TEST_USE_REVERSE_EDGE, "label");


            GraphViewCommand graphCommand = new GraphViewCommand(connection);

            graphCommand.g().AddV("person").Property("name", "marko").Property("age", 29).Next();
            graphCommand.g().AddV("person").Property("name", "vadas").Property("age", 27).Next();
            graphCommand.g().AddV("software").Property("name", "lop").Property("lang", "java").Next();
            graphCommand.g().AddV("person").Property("name", "josh").Property("age", 32).Next();
            graphCommand.g().AddV("software").Property("name", "ripple").Property("lang", "java").Next();
            graphCommand.g().AddV("person").Property("name", "peter").Property("age", 35).Next();
            graphCommand.g().V().Has("name", "marko").AddE("knows").Property("weight", 0.5d).To(graphCommand.g().V().Has("name", "vadas")).Next();
            graphCommand.g().V().Has("name", "marko").AddE("knows").Property("weight", 1.0d).To(graphCommand.g().V().Has("name", "josh")).Next();
            graphCommand.g().V().Has("name", "marko").AddE("created").Property("weight", 0.4d).To(graphCommand.g().V().Has("name", "lop")).Next();
            graphCommand.g().V().Has("name", "josh").AddE("created").Property("weight", 1.0d).To(graphCommand.g().V().Has("name", "ripple")).Next();
            graphCommand.g().V().Has("name", "josh").AddE("created").Property("weight", 0.4d).To(graphCommand.g().V().Has("name", "lop")).Next();
            graphCommand.g().V().Has("name", "peter").AddE("created").Property("weight", 0.2d).To(graphCommand.g().V().Has("name", "lop")).Next();

            graphCommand.Dispose();
            connection.Dispose();
        }

        private static void LoadModernGraphData()
        {

            //string endpoint = ConfigurationManager.AppSettings["DocDBEndPoint"];
            string endpoint = ConfigurationManager.AppSettings["DocDBEndPointLocal"];
            //string authKey = ConfigurationManager.AppSettings["DocDBKey"];
            string authKey = ConfigurationManager.AppSettings["DocDBKeyLocal"];
            string databaseId = ConfigurationManager.AppSettings["DocDBDatabaseGremlin"];
            string collectionId = ConfigurationManager.AppSettings["DocDBCollectionModern"];

            GraphViewConnection connection = GraphViewConnection.ResetGraphAPICollection(endpoint, authKey, databaseId, collectionId, AbstractGremlinTest.TEST_USE_REVERSE_EDGE, "label");


            GraphViewCommand graphCommand = new GraphViewCommand(connection);

            //
            // NOTE: '#' charactor is now allowed in document id
            // NOTE: '\' charactor is now allowed in document id
            // NOTE: '?' charactor is now allowed in document id
            // NOTE: '/' charactor is now allowed in document id
            // NOTE: ' (single quote) charactor will cause an error now
            //
            //graphCommand.g().AddV("person").Property("id", "dummy!").Property("name", "marko").Property("age", 29).Next();
            //graphCommand.g().AddV("person").Property("id", "特殊符号:~!@$%^&*()_+").Property("name", "vadas").Property("age", 27).Next();
            //graphCommand.g().AddV("software").Property("id", "这是一个中文ID").Property("name", "lop").Property("lang", "java").Next();
            //graphCommand.g().AddV("person").Property("id", "引号\"`").Property("name", "josh").Property("age", 32).Next();
            //graphCommand.g().AddV("software").Property("id", "中文English(){}[]<>\"`~!@$%^^&*()_+-=|:;,.").Property("name", "ripple").Property("lang", "java").Next();
            //graphCommand.g().AddV("person").Property("name", "peter").Property("age", 35).Next();
            //graphCommand.g().V().Has("name", "marko").AddE("knows").Property("weight", 0.5d).To(graphCommand.g().V().Has("name", "vadas")).Next();
            //graphCommand.g().V().Has("name", "marko").AddE("knows").Property("weight", 1.0d).To(graphCommand.g().V().Has("name", "josh")).Next();
            //graphCommand.g().V().Has("name", "marko").AddE("created").Property("weight", 0.4d).To(graphCommand.g().V().Has("name", "lop")).Next();
            //graphCommand.g().V().Has("name", "josh").AddE("created").Property("weight", 1.0d).To(graphCommand.g().V().Has("name", "ripple")).Next();
            //graphCommand.g().V().Has("name", "josh").AddE("created").Property("weight", 0.4d).To(graphCommand.g().V().Has("name", "lop")).Next();
            //graphCommand.g().V().Has("name", "peter").AddE("created").Property("weight", 0.2d).To(graphCommand.g().V().Has("name", "lop")).Next();

            graphCommand.g().AddV("person").Property("id", "dummy").Property("name", "marko").Property("age", 29).Next();
            graphCommand.g().AddV("person").Property("id", "特殊符号").Property("name", "vadas").Property("age", 27).Next();
            graphCommand.g().AddV("software").Property("id", "这是一个中文ID").Property("name", "lop").Property("lang", "java").Next();
            graphCommand.g().AddV("person").Property("id", "引号").Property("name", "josh").Property("age", 32).Next();
            graphCommand.g().AddV("software").Property("id", "中文English").Property("name", "ripple").Property("lang", "java").Next();
            graphCommand.g().AddV("person").Property("name", "peter").Property("age", 35).Next();  // Auto generate document id
            graphCommand.g().V().Has("name", "marko").AddE("knows").Property("weight", 0.5d).To(graphCommand.g().V().Has("name", "vadas")).Next();
            graphCommand.g().V().Has("name", "marko").AddE("knows").Property("weight", 1.0d).To(graphCommand.g().V().Has("name", "josh")).Next();
            graphCommand.g().V().Has("name", "marko").AddE("created").Property("weight", 0.4d).To(graphCommand.g().V().Has("name", "lop")).Next();
            graphCommand.g().V().Has("name", "josh").AddE("created").Property("weight", 1.0d).To(graphCommand.g().V().Has("name", "ripple")).Next();
            graphCommand.g().V().Has("name", "josh").AddE("created").Property("weight", 0.4d).To(graphCommand.g().V().Has("name", "lop")).Next();
            graphCommand.g().V().Has("name", "peter").AddE("created").Property("weight", 0.2d).To(graphCommand.g().V().Has("name", "lop")).Next();

            graphCommand.Dispose();
            connection.Dispose();
        }

        private static void ClearGraphData(string CollectionName)
        {
            //string endpoint = ConfigurationManager.AppSettings["DocDBEndPoint"];
            string endpoint = ConfigurationManager.AppSettings["DocDBEndPointLocal"];
            //string authKey = ConfigurationManager.AppSettings["DocDBKey"];
            string authKey = ConfigurationManager.AppSettings["DocDBKeyLocal"];
            string databaseId = ConfigurationManager.AppSettings["DocDBDatabaseGremlin"];

            GraphViewConnection.ResetGraphAPICollection(endpoint, authKey, databaseId, CollectionName, AbstractGremlinTest.TEST_USE_REVERSE_EDGE/*, "name"*/);
        }
    }
}
