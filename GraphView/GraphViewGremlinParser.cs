﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GraphView;
using GraphView.TSQL_Syntax_Tree;
using Microsoft.SqlServer.TransactSql.ScriptDom;

/* 
 *  BNF for the Gremlin Language
 *  
 *  S → Path
 *  T → Identifier | Keyword | QuotedString | Number
 *  
 *  Path ::= Identifier "=" Fragment | Fragment
 *  Fragment ::= Function | Function"."Fragment | Identifier | Identifier"."Fragment
 *  Function ::= Keyword"("Parameters")" | Keyword
 *  Parameters ::= Parameter | Parameter","Parameters
 *  Parameter ::= Fragment | QuotedString | Number | Function
 *  
*/
namespace GraphView
{
    public class GraphViewGremlinParser
    {
        internal enum TokenType
        {
            Null,
            LeftParenthesis,
            RightParenthesis,
            LeftBrace,
            RightBrace,
            Integer,
            Double,
            Binary,
            NodeType,
            DoubleQuotedString,
            SingleQuotedString,
            ArithmeticOperator1,
            ArithmeticOperator2,
            ComparisonOperator,
            BindOperator,
            Colon,
            NameToken,
            DotToken,
            Comma,
            LeftBracket,
            RightBracket,
            ValueToken,
            AsToken,
            Space,
            Keyword,
            Identifier,
        };
        internal enum Keywords
        {
            addOutE,         // Supported
            addInE,          // Supported
            addV,            // Supported
            aggregate,       // Supported
            and,             // Supported
            As,              // Supported
            by,              // Group-by requested
            cap,             // Group-by requested
            coalesce,        // Supported
            count,           // Group-by requested
            choose,          // Supported
            coin,            // Group-by requested
            drop,            // Supported
            fold,            // Mapping requested
            group,           // Group-by requested
            groupCount,      // Group-by requested
            has,             // Supported
            inject,          // Updating on Nodes and Edges requested
            limit,           // Supported
            mapKeys,         // Mapping requested
            mapValues,       // Mapping requested
            match,           // Supported
            max,             // Group-by requested
            mean,            // Group-by requested
            min,             // Group-by requested
            or,              // Supported
            order,           // Group-by requested
            repeat,          // Supported
            select,          // Supported
            store,           // Supported
            sum,             // Group-by requested
            tail,            // Group-by requested
            tree,            // Group-by requested
            unfold,          // Group-by requested
            union,           // Group-by requested
            valueMap,        // Mapping requested
            Out,             // Supported
            In,              // Supported
            both,            // Supported
            outE,            // Supported
            inE,             // Supported
            bothE,           // Supported
            outV,            // Supported
            inV,             // Supported
            bothV,           // Supported
            otherV,          // Supported
            where,           // Supported
            values,          // Supported
            label,           // Supported
            V,               // Supported
            E,               // Supported
            next,            // Supported
            g,               // Supported
            eq,              // Supported
            neq,             // Supported
            lt,              // Supported
            lte,             // Supported
            gt,              // Supported
            gte,             // Supported
            placeholder,     // Supported
            times,           // Supported
            option,          // Supported   
            Is               // Supported 
        }
        internal static Dictionary<String, Keywords> KeyWordDic = new Dictionary<string, Keywords>(StringComparer.OrdinalIgnoreCase)
        {
           {"addOutE", Keywords.addOutE},
           {"addInE", Keywords.addInE},
           {"addV", Keywords.addV},
           {"aggregate", Keywords.aggregate},
           {"and", Keywords.and},
           {"as", Keywords.As},
           {"by", Keywords.by},
           {"cap", Keywords.cap},
           {"coalesce", Keywords.coalesce},
           {"count", Keywords.count},
           {"choose", Keywords.choose},
           {"coin", Keywords.coin},
           {"drop", Keywords.drop},
           {"fold", Keywords.fold},
           {"group", Keywords.group},
           {"groupCount", Keywords.groupCount},
           {"has", Keywords.has},
           {"inject", Keywords.inject},
           {"limit", Keywords.limit},
           {"mapKeys", Keywords.mapKeys},
           {"mapValues", Keywords.mapValues},
           {"match", Keywords.match},
           {"max", Keywords.max},
           {"mean", Keywords.mean},
           {"min", Keywords.min},
           {"or", Keywords.or},
           {"order", Keywords.order},
           {"repeat", Keywords.repeat},
           {"select", Keywords.select},
           {"store", Keywords.store},
           {"sum", Keywords.sum},
           {"tail", Keywords.tail},
           {"tree", Keywords.tree},
           {"unfold", Keywords.unfold},
           {"union", Keywords.union},
           {"valueMap", Keywords.valueMap},
           {"Out", Keywords.Out},
           {"In", Keywords.In},
           {"both", Keywords.both},
           {"outE", Keywords.outE},
           {"inE", Keywords.inE},
           {"bothE", Keywords.bothE},
           {"outV", Keywords.outV},
           {"inV", Keywords.inV},
           {"bothV", Keywords.bothV},
           {"otherV", Keywords.otherV},
           {"where", Keywords.where},
           {"values" ,Keywords.values},
           {"label",Keywords.label },
           {"V", Keywords.V },
           {"E", Keywords.E },
           {"g",Keywords.g },
           {"next", Keywords.next },
           {"eq",Keywords.eq},
            {"neq",Keywords.neq},
            {"lt",Keywords.lt},
            {"lte",Keywords.lte},
            {"gt",Keywords.gt},
            {"gte",Keywords.gte},
            {"__",Keywords.placeholder},
            {"times",Keywords.times},
            {"option",Keywords.option },
             {"Is",Keywords.Is }
        };
        internal class Token
        {
            public TokenType type;
            public string value;
            public int position;
        }

        internal class Keyword : Token
        {
            public Keywords KeywordIndex;
        }

        internal class Identifier : Token
        {
            public int IdentifierIndex;
        }


        public WSqlStatement Parse(string Script)
        {
            string ErrorKey = "";
            var para = GraphViewGremlinParser.LexicalAnalyzer.Tokenize(Script, ref ErrorKey);
            Identifiers = para.Item2;
            TokenList = para.Item1;
            NextToken = 0;
            FarestError = -1;
            var ParserTree = ParseTree();
            var SematicAnalyser = new GraphViewGremlinSematicAnalyser(ParserTree, para.Item2);
            SematicAnalyser.Analyse();
            if (SematicAnalyser.SematicContext.BranchContexts.Count != 0 && SematicAnalyser.SematicContext.BranchNote != null)
            {
                var choose = new WChoose() {InputExpr = new List<WSelectQueryBlock>()};
                foreach (var x in SematicAnalyser.SematicContext.BranchContexts)
                {
                    var branch = SematicAnalyser.Transform(x);
                    choose.InputExpr.Add(branch as WSelectQueryBlock);
                }
                SqlTree = choose;
            }
            else if (SematicAnalyser.SematicContext.BranchContexts.Count != 0 && SematicAnalyser.SematicContext.BranchNote == null)
            {
                var choose = new WCoalesce() { InputExpr = new List<WSelectQueryBlock>(),CoalesceNumber = SematicAnalyser.SematicContext.NodeCount};
                foreach (var x in SematicAnalyser.SematicContext.BranchContexts)
                {
                    var branch = SematicAnalyser.Transform(x);
                    choose.InputExpr.Add(branch as WSelectQueryBlock);
                }
                SqlTree = choose;
            }
            else 
            {
                SematicAnalyser.Transform(SematicAnalyser.SematicContext);
                SqlTree = SematicAnalyser.SqlTree;
            }
            return SqlTree;
        }
        internal static class LexicalAnalyzer
        {
            internal static Dictionary<Regex, TokenType> TokenRules = null;

            internal static void Initialize()
            {
                TokenRules = new Dictionary<Regex, TokenType>
                {
                    {new Regex(@"\("), TokenType.LeftParenthesis},
                    {new Regex(@"\)"), TokenType.RightParenthesis},
                    {new Regex(@"{"), TokenType.LeftBrace},
                    {new Regex(@"}"), TokenType.RightBrace},
                    {new Regex(@"(\+|-|~|&|\^|\|)"), TokenType.ArithmeticOperator1},
                    {new Regex(@"(\*|/|%)"), TokenType.ArithmeticOperator2},
                    {new Regex(@"(\<=|\>=|=|\<\>|\<|\>|!\>|!\<|!=)"), TokenType.ComparisonOperator},
                    {new Regex(@":="), TokenType.BindOperator},
                    {new Regex(@":"), TokenType.Colon},
                    {new Regex(@"(0[x|X][0-9a-fA-F]+)"), TokenType.Binary},
                    {new Regex(@"([0-9]+\.[0-9]+)"), TokenType.Double},
                    {new Regex(@"([0-9]+)"), TokenType.Integer},
                    {new Regex(@"(""([^""\\]|\\.)*"")"), TokenType.DoubleQuotedString},
                    {new Regex(@"('([^'\\]|\\.)*')"), TokenType.SingleQuotedString},
                    {new Regex(@"([a-zA-Z_][0-9a-zA-Z_]*)"), TokenType.NameToken},
                    {new Regex(@"\."), TokenType.DotToken},
                    {new Regex(@"\,"), TokenType.Comma},
                    {new Regex(@"\["), TokenType.LeftBracket},
                    {new Regex(@"\]"), TokenType.RightBracket},
                    {new Regex(@"(\s+)"), TokenType.Space}
                };
            }
            /// <summary>
            /// Tokenize the input script
            /// </summary>
            internal static Tuple<List<Token>, List<string>> Tokenize(string text, ref string ErrorKey)
            {
                if (TokenRules == null) Initialize();

                List<string> Identifiers = new List<string>();
                List<Token> tokens = new List<Token>();
                int position = 0;
                int LastPosition = position;
                TokenType Type;
                while (position < text.Length)
                {
                    Type = TokenType.Null;
                    // Find the fitest token type
                    var result = TokenRules
                        .Select(p => Tuple.Create(p.Key.Match(text, position), p.Value))
                        .FirstOrDefault(t => t.Item1.Index == position && t.Item1.Success);
                    if (result == null)
                    {
                        ErrorKey = text.Substring(LastPosition, position - LastPosition + 1);
                        return null;
                    }

                    // Mark as Keyword
                    if (KeyWordDic.ContainsKey(result.Item1.Value)) Type = TokenType.Keyword;
                    // Mark as Identifier
                    if (result.Item2 == TokenType.NameToken && !KeyWordDic.ContainsKey(result.Item1.Value))
                        Type = TokenType.Identifier;
                    // Mark as other token type
                    if (Type == TokenType.Null)
                        Type = result.Item2;

                    if (Type != TokenType.Space)
                    {
                        // add double quoted string
                        if (Type == TokenType.DoubleQuotedString)
                        {
                            tokens.Add(new Token()
                            {
                                type = result.Item2,
                                value = result.Item1.Value.Substring(1, result.Item1.Value.Length - 2),
                                position = position
                            });
                        }
                        // add keyword
                        if (Type == TokenType.Keyword)
                        {
                            tokens.Add(new Keyword()
                            {
                                type = Type,
                                value = result.Item1.Value,
                                position = position,
                                KeywordIndex = KeyWordDic[result.Item1.Value]
                            });
                        }
                        // add identifier
                        else if (Type == TokenType.Identifier)
                        {
                            int identifier = -1;
                            if (Identifiers.Contains((result.Item1.Value)))
                                identifier = Identifiers.IndexOf((result.Item1.Value));
                            else
                            {
                                Identifiers.Add(result.Item1.Value);
                                identifier = Identifiers.IndexOf((result.Item1.Value));
                            }
                            tokens.Add(new Identifier()
                            {
                                type = Type,
                                value = result.Item1.Value,
                                position = position,
                                IdentifierIndex = identifier
                            });
                        }
                        else
                        {
                            // add other token
                            tokens.Add(new Token()
                            {
                                type = Type,
                                value = result.Item1.Value,
                                position = position
                            });
                        }
                    }
                    position += result.Item1.Length;
                    LastPosition = position;
                }
                return new Tuple<List<Token>, List<string>>(tokens, Identifiers);
            }
        }

        /// <summary>
        /// Read a spcific token by type at pNextToken, and return the value of it
        /// </summary>
        internal static bool ReadToken(
            List<Token> tokens,
            TokenType type,
            ref int pNextToken,
            ref string TokenValue,
            ref int pFarestError)
        {
            if (tokens.Count == pNextToken)
            {
                pFarestError = pNextToken;
                return false;
            }
            else if (tokens[pNextToken].type == type)
            {
                TokenValue = tokens[pNextToken].value;
                pNextToken++;
                return true;
            }
            else
            {
                pFarestError = Math.Max(pFarestError, pNextToken);
                return false;
            }
        }
        /// <summary>
        /// Read a spcific token by type at pNextToken, and return the index of keyword/identifier
        /// </summary>
        internal static bool ReadToken(
    List<Token> tokens,
    TokenType type,
    ref int pNextToken,
    ref int TokenIndex,
    ref int pFarestError)
        {
            if (tokens.Count == pNextToken)
            {
                pFarestError = pNextToken;
                return false;
            }
            else if (tokens[pNextToken].type == type)
            {
                if (tokens[pNextToken].type == TokenType.Identifier)
                    TokenIndex = (tokens[pNextToken] as Identifier).IdentifierIndex;
                if (tokens[pNextToken].type == TokenType.Keyword)
                    TokenIndex = (int)((tokens[pNextToken] as Keyword).KeywordIndex);
                pNextToken++;
                return true;
            }
            else
            {
                pFarestError = Math.Max(pFarestError, pNextToken);
                return false;
            }
        }
        /// <summary>
        /// Read a spcific token by value at pNextToken
        /// </summary>
        internal static bool ReadToken(
            List<Token> tokens,
            string text,
            ref int pNextToken,
            ref string TokenValue,
            ref int pFarestError)
        {
            if (tokens.Count == pNextToken)
            {
                pFarestError = pNextToken;
                return false;
            }
            else if (string.Equals(tokens[pNextToken].value, text, StringComparison.OrdinalIgnoreCase))
            {
                TokenValue = tokens[pNextToken].value;
                pNextToken++;
                return true;
            }
            else
            {
                pFarestError = Math.Max(pFarestError, pNextToken);
                return false;
            }
        }

        internal List<string> Identifiers;
        internal List<Token> TokenList;
        internal int NextToken;
        internal int FarestError;
        internal WSqlStatement SqlTree;

        internal GraphViewGremlinParser(List<Token> pTokens, List<string> pIdentifiers)
        {
            Identifiers = pIdentifiers;
            TokenList = pTokens;
            NextToken = 0;
            FarestError = -1;
        }

        internal GraphViewGremlinParser()
        {
            NextToken = 0;
            FarestError = -1;
        }
        internal WProgram ParseTree()
        {
            WPath pPath;
            WProgram result = new WProgram();
            result.paths = new List<WPath>();
            while ((pPath = ParsePath()) != null)
            {
                result.paths.Add((pPath));
            }
            return result;
        }
        internal WPath ParsePath()
        {
            int pIdentifier = ParseIdentifier();
            bool pEqual = ParseEqual();
            WFragment pFragment = ParseFragment();
            if (pIdentifier != null && pEqual && pFragment != null)
                return new WPath() { Fragment = pFragment, IdentifierIndex = pIdentifier };
            if (pFragment != null)
                return new WPath() { Fragment = pFragment, IdentifierIndex = -1 };
            return null;

        }

        internal WFragment ParseFragment()
        {
            WFunction pFunction = ParseFunction();
            bool pFullStop = ParseFullStop();
            if (pFullStop)
            {
                WFragment pFragment = ParseFragment();
                if (pFunction != null && pFullStop && pFragment != null)
                    return new WFragment() { Fragment = pFragment, Function = pFunction, Identifer = -1 };
            }
            if (pFunction != null)
                return new WFragment() { Function = pFunction, Identifer = -1 };
            int pIdentifier = ParseIdentifier();
            pFullStop = ParseFullStop();
            if (pFullStop)
            {
                WFragment pFragment = ParseFragment();
                if (pIdentifier != -1 && pFullStop && pFragment != null)
                    return new WFragment() { Identifer = pIdentifier, Fragment = pFragment };
            }
            if (pIdentifier != -1)
            {
                WFragment pFragment = ParseFragment();
                if (pIdentifier != null && pFullStop && pFragment != null)
                    return new WFragment() { Fragment = pFragment, Function = pFunction, Identifer = -1 };
                else 
                return new WFragment() {Identifer = pIdentifier, Fragment = pFragment};
            }

            return null;
        }

        internal WFunction ParseFunction()
        {
            int pKeyword = ParseKeyword();
            bool pLeftBrace = ParseLeftBrace();
            if (pLeftBrace)
            {
                WParameters pParameters = ParseParameters();
                bool pRightBrace = ParseRightBrace();
                if (pKeyword != -1 && pLeftBrace && pParameters != null && pRightBrace)
                    return new WFunction() { KeywordIndex = pKeyword, Parameters = pParameters };
                if (pKeyword != -1 && pLeftBrace && pRightBrace)
                    return new WFunction() { KeywordIndex = pKeyword};
            }
            if (pKeyword != -1)
                return new WFunction() { KeywordIndex = pKeyword };
            return null;
        }

        internal WParameters ParseParameters()
        {
            WParameters result = new WParameters();
            WParameter pParameter;
            result.Parameter = new List<WParameter>();
            // the quote of the parameter will be cut off here
            while ((pParameter = ParseParameter()) != null && ParseColon())
            {
                if (pParameter.QuotedString != null && pParameter.QuotedString.First() == '\'' && pParameter.QuotedString.Last() == '\'')
                    pParameter.QuotedString = pParameter.QuotedString.Substring(1, pParameter.QuotedString.Length - 2);
                result.Parameter.Add(pParameter);
            }
            if (pParameter != null)
            {
                if (pParameter.QuotedString != null && pParameter.QuotedString.First() == '\'' && pParameter.QuotedString.Last() == '\'')
                    pParameter.QuotedString = pParameter.QuotedString.Substring(1, pParameter.QuotedString.Length - 2);
                result.Parameter.Add(pParameter);
                return result;
            }
            return null;

        }

        internal WParameter ParseParameter()
        {
            double pConstValue = ParseNumber();
            if (!Double.IsNaN(pConstValue)) return new WParameter() { Number = pConstValue, IdentifierIndex = -1 };
            string pQuotedString = ParseQuotedString();
            if (pQuotedString != null) return new WParameter() { QuotedString = pQuotedString, IdentifierIndex = -1, Number = double.NaN };
            int pidentifier = ParseIdentifier();
            if (pidentifier != -1) return new WParameter() { IdentifierIndex = pidentifier, Number = double.NaN };
            WFragment pFragment = ParseFragment();
            if (pFragment != null) return new WParameter() { Fragment = pFragment, IdentifierIndex = -1, Number = double.NaN };
            return null;
        }
        internal double ParseNumber()
        {
            // All the number value will be return as a double.
            string value = "";
            if (ReadToken(TokenList, TokenType.Double, ref NextToken, ref value, ref FarestError))
                return double.Parse(value);
            if (ReadToken(TokenList, TokenType.Integer, ref NextToken, ref value, ref FarestError))
                return Int32.Parse(value);
            else return double.NaN;
        }
        internal int ParseIdentifier()
        {
            int index = 0;
            if (ReadToken(TokenList, TokenType.Identifier, ref NextToken, ref index, ref FarestError))
                return index;
            else return -1;
        }

        internal int ParseKeyword()
        {
            int index = 0;
            if (ReadToken(TokenList, TokenType.Keyword, ref NextToken, ref index, ref FarestError))
                return index;
            else return -1;
        }

        internal string ParseQuotedString()
        {
            string QuotedString = "";
            if (ReadToken(TokenList, TokenType.SingleQuotedString, ref NextToken, ref QuotedString, ref FarestError))
                return QuotedString;
            else return null;
        }

        internal bool ParseEqual()
        {
            string temp = "";
            return (ReadToken(TokenList, "=", ref NextToken, ref temp, ref FarestError));
        }

        internal bool ParseLeftBrace()
        {
            string temp = "";
            return (ReadToken(TokenList, "(", ref NextToken, ref temp, ref FarestError));
        }

        internal bool ParseRightBrace()
        {
            string temp = "";
            return (ReadToken(TokenList, ")", ref NextToken, ref temp, ref FarestError));
        }

        internal bool ParseColon()
        {
            string temp = "";
            return (ReadToken(TokenList, ",", ref NextToken, ref temp, ref FarestError));
        }

        internal bool ParseFullStop()
        {
            string temp = "";
            return (ReadToken(TokenList, ".", ref NextToken, ref temp, ref FarestError));
        }

        internal bool ParseSpliter()
        {
            string temp = "";
            return (ReadToken(TokenList, ";", ref NextToken, ref temp, ref FarestError));
        }
    }

    public class GraphViewGremlinSematicAnalyser
    {
        // Internal representation of the traversal statue, which maintains a statue of current "traversal steps"
        internal class Context
        {
            internal List<string> PrimaryInternalAlias { get; set; }
            internal List<string> InternalAliasList { get; set; }
            internal List<List<string>> AliasPredicates { get; set; }
            internal List<Tuple<string, string, string>> Paths { get; set; }
            internal List<string> Identifiers { get; set; }
            internal Dictionary<string, string> Properties { get; set; }
            internal Dictionary<string, string> ExplictAliasToInternalAlias { get; set; }
            internal List<Context> BranchContexts;
            internal string BranchNote;
            internal int NodeCount;
            internal int EdgeCount;
            internal bool AddEMark;
            internal bool AddVMark;
            internal bool RemoveMark;
            internal bool ChooseMark;
            internal int limit;

            internal Context(Context rhs)
            {
                PrimaryInternalAlias = new List<string>();
                foreach(var x in rhs.PrimaryInternalAlias)
                PrimaryInternalAlias.Add(x);
                InternalAliasList = new List<string>();
                foreach (var x in rhs.InternalAliasList)
                    InternalAliasList.Add(x);
                AliasPredicates = new List<List<string>>();
                foreach (var x in rhs.AliasPredicates)
                {
                    AliasPredicates.Add(new List<string>());
                    foreach (var y in x)
                    {
                        AliasPredicates.Last().Add(y);
                    }
                }
                Paths = new List<Tuple<string, string, string>>();
                foreach (var x in rhs.Paths)
                {
                    Paths.Add(x);
                }
                Identifiers = new List<string>();
                foreach (var x in rhs.Identifiers)
                {
                    Identifiers.Add(x);
                }
                Properties = new Dictionary<string, string>();
                foreach (var x in rhs.Properties)
                {
                    Properties.Add(x.Key,x.Value);
                }
                ExplictAliasToInternalAlias = new Dictionary<string, string>();
                foreach (var x in rhs.ExplictAliasToInternalAlias)
                {
                    ExplictAliasToInternalAlias.Add(x.Key, x.Value);
                }
                if(rhs.BranchContexts != null)
                foreach (var x in rhs.BranchContexts)
                {
                    BranchContexts.Add(x);
                }
                BranchNote = rhs.BranchNote;
                NodeCount = rhs.NodeCount;
                EdgeCount = rhs.EdgeCount;
                AddEMark = rhs.AddEMark;
                AddVMark = rhs.AddVMark;
                RemoveMark = rhs.RemoveMark;
                ChooseMark = rhs.ChooseMark;
            }

            internal Context()
            {
                PrimaryInternalAlias = new List<string>();
                InternalAliasList = new List<string>();
                AliasPredicates = new List<List<string>>();
                Paths = new List<Tuple<string, string, string>>();
                ExplictAliasToInternalAlias = new Dictionary<string, string>();
                NodeCount = 0;
                EdgeCount = 0;
                AddEMark = false;
                AddVMark = false;
                RemoveMark = false;
                ChooseMark = false;
                Properties = new Dictionary<string, string>();
                limit = -1;
                BranchContexts = new List<Context>();
                Identifiers = new List<string>();
            }
        }

        internal Context SematicContext;
        // Gremlin syntax tree
        internal WProgram ParserTree;
        // TSQL(Graphview) syntax tree
        internal WSqlStatement SqlTree;
        internal GraphViewGremlinSematicAnalyser(WProgram pParserTree, List<string> pIdentifiers)
        {
            SematicContext = new Context()
            {
                PrimaryInternalAlias = new List<string>(),
                InternalAliasList = new List<string>(),
                AliasPredicates = new List<List<string>>(),
                Paths = new List<Tuple<string, string, string>>(),
                Identifiers = pIdentifiers,
                ExplictAliasToInternalAlias = new Dictionary<string, string>(),
                NodeCount = 0,
                EdgeCount = 0,
                AddEMark = false,
                AddVMark = false,
                RemoveMark = false,
                ChooseMark = false,
                Properties = new Dictionary<string, string>(),
                limit = -1,
                BranchContexts = new List<Context>()
            };
            ParserTree = pParserTree;
        }

        internal GraphViewGremlinSematicAnalyser()
        {
            
        }

        public void Analyse()
        {
            ParserTree.Transform(ref SematicContext);
        }

        // Transform the Gremlin Parser Tree into TSQL Parser tree
        internal WSqlStatement Transform(Context context)
        {
            var SelectStatement = new WSelectStatement();
            var SelectBlock = SelectStatement.QueryExpr as WSelectQueryBlock;

            // Consturct the new From Clause
            var NewFromClause = new WFromClause() { TableReferences = new List<WTableReference>() };
            foreach (var a in context.InternalAliasList)
            {
                if (a.Contains("N_"))
                {
                    var TR = new WNamedTableReference()
                    {
                        Alias = new Identifier() { Value = a },
                        TableObjectString = "node",
                        TableObjectName = new WSchemaObjectName(new Identifier() { Value = "node" })
                    };
                    NewFromClause.TableReferences.Add(TR);
                }
            }

            // Consturct the new Match Clause
            var NewMatchClause = new WMatchClause() { Paths = new List<WMatchPath>() };
            foreach (var path in context.Paths)
            {
                var PathEdges = new List<Tuple<WSchemaObjectName, WEdgeColumnReferenceExpression>>();
                PathEdges.Add(new Tuple<WSchemaObjectName, WEdgeColumnReferenceExpression>
                    (new WSchemaObjectName()
                    {
                        Identifiers = new List<Identifier>()
                        {
                                new Identifier() {Value = path.Item1}
                        }
                    },
                        new WEdgeColumnReferenceExpression()
                        {
                            MultiPartIdentifier =
                                new WMultiPartIdentifier()
                                {
                                    Identifiers = new List<Identifier>() { new Identifier() { Value = "Edge" } }
                                },
                            Alias = path.Item2
                        }));
                var TailNode = new WSchemaObjectName()
                {
                    Identifiers = new List<Identifier>()
                            {
                                new Identifier() {Value = path.Item3}
                            }
                };
                var NewPath = new WMatchPath() { PathEdgeList = PathEdges, Tail = TailNode };
                NewMatchClause.Paths.Add((NewPath));
            }

            // Consturct the new Select Component

            var NewSelectElementClause = new List<WSelectElement>();
            foreach (var alias in context.PrimaryInternalAlias)
            {
                var pIdentifiers = new List<Identifier>();
                var TempString = alias;
                while (TempString.IndexOf('.') != -1)
                {
                    int cutpoint = TempString.IndexOf('.');
                    pIdentifiers.Add(new Identifier() { Value = TempString.Substring(0, cutpoint) });
                    TempString = TempString.Substring(cutpoint + 1, TempString.Length - cutpoint - 1);
                }
                pIdentifiers.Add(new Identifier() { Value = TempString });
                var SelectExpr = new WColumnReferenceExpression() { MultiPartIdentifier = new WMultiPartIdentifier() { Identifiers = pIdentifiers } };
                var element = new WSelectScalarExpression() { SelectExpr = SelectExpr };
                NewSelectElementClause.Add(element);
            }

            // Consturct the new Where Clause

            var NewWhereClause = new WWhereClause();
            var Condition = new WBooleanBinaryExpression();
            List<WBooleanExpression> BooleanList = new List<WBooleanExpression>();
            foreach (var exprs in context.AliasPredicates)
            {
                foreach (var expr in exprs)
                {
                    if (expr == "") continue;
                    int cutpoint = 0;
                    BooleanComparisonType CompType = BooleanComparisonType.Equals;
                    if (expr.IndexOf('=') != -1)
                    {
                        cutpoint = expr.IndexOf('=');
                        CompType = BooleanComparisonType.Equals;
                    }
                    if (expr.IndexOf('<') != -1)
                    {
                        cutpoint = expr.IndexOf('<');
                        CompType = BooleanComparisonType.LessThan;
                    }
                    if (expr.IndexOf('>') != -1)
                    {
                        cutpoint = expr.IndexOf('>');
                        CompType = BooleanComparisonType.GreaterThan;
                    }
                    if (expr.IndexOf('[') != -1)
                    {
                        cutpoint = expr.IndexOf('[');
                        CompType = BooleanComparisonType.LessThanOrEqualTo;
                    }
                    if (expr.IndexOf(']') != -1)
                    {
                        cutpoint = expr.IndexOf(']');
                        CompType = BooleanComparisonType.GreaterThanOrEqualTo;
                    }
                    if (expr.IndexOf('!') != -1)
                    {
                        cutpoint = expr.IndexOf('!');
                        CompType = BooleanComparisonType.NotEqualToExclamation;
                    }
                    string firstExpr = expr.Substring(0, cutpoint);
                    string secondExpr = expr.Substring(cutpoint + 2, expr.Length - cutpoint - 2);
                    var pIdentifiers = new List<Identifier>();
                    while (firstExpr.IndexOf('.') != -1)
                    {
                        int cutpoint2 = firstExpr.IndexOf('.');
                        pIdentifiers.Add(new Identifier() {Value = firstExpr.Substring(0, cutpoint2)});
                        firstExpr = firstExpr.Substring(cutpoint2 + 1, firstExpr.Length - cutpoint2 - 1);
                    }
                    pIdentifiers.Add(new Identifier() {Value = firstExpr});
                    var FirstRef = new WColumnReferenceExpression()
                    {
                        MultiPartIdentifier = new WMultiPartIdentifier() {Identifiers = pIdentifiers}
                    };
                    pIdentifiers = new List<Identifier>();
                    while (secondExpr.IndexOf('.') != -1)
                    {
                        int cutpoint2 = secondExpr.IndexOf('.');
                        pIdentifiers.Add(new Identifier() {Value = secondExpr.Substring(0, cutpoint2)});
                        secondExpr = secondExpr.Substring(cutpoint2 + 1, secondExpr.Length - cutpoint2 - 1);
                    }
                    pIdentifiers.Add(new Identifier() {Value = secondExpr});
                    double temp;
                    WScalarExpression SecondRef = null;
                    if (pIdentifiers.Count > 1)
                        SecondRef = new WColumnReferenceExpression()
                        {
                            MultiPartIdentifier = new WMultiPartIdentifier() {Identifiers = pIdentifiers}
                        };
                    else
                    {
                        if (double.TryParse(secondExpr, out temp))
                            SecondRef = new WValueExpression(secondExpr, false);
                        else
                            SecondRef = new WValueExpression(secondExpr, true);
                    }
                WBooleanComparisonExpression BBE = new WBooleanComparisonExpression()
                    {
                        ComparisonType = CompType,
                        FirstExpr = FirstRef,
                        SecondExpr = SecondRef
                    };
                    BooleanList.Add(BBE);
                }
            }
            if (BooleanList.Count > 0)
            {
                if (BooleanList.Count == 1)
                {
                    NewWhereClause = new WWhereClause() { SearchCondition = BooleanList[0] };
                }
                else
                {
                    Condition = new WBooleanBinaryExpression()
                    {
                        BooleanExpressionType = BooleanBinaryExpressionType.And,
                        FirstExpr = BooleanList[0],
                        SecondExpr = BooleanList[1]
                    };
                    for (int i = 2; i < BooleanList.Count; i++)
                    {
                        Condition = new WBooleanBinaryExpression()
                        {
                            BooleanExpressionType = BooleanBinaryExpressionType.And,
                            FirstExpr = Condition,
                            SecondExpr = BooleanList[i]
                        };
                    }
                    NewWhereClause = new WWhereClause() { SearchCondition = Condition };
                }
            }

            SelectBlock = new WSelectQueryBlock()
            {
                FromClause = NewFromClause,
                SelectElements = NewSelectElementClause,
                WhereClause = NewWhereClause,
                MatchClause = NewMatchClause
            };

            SqlTree = SelectBlock;

            // If needed to add vertex, consturct new InsertNodeStatement
            if (context.AddVMark)
            {
                var columnV = new List<WScalarExpression>();
                var columnK = new List<WColumnReferenceExpression>();
                foreach (var property in context.Properties)
                {
                    var value = new WValueExpression(property.Value, true);
                    columnV.Add(value);
                    var key = new WColumnReferenceExpression()
                    {
                        MultiPartIdentifier =
                            new WMultiPartIdentifier()
                            {
                                Identifiers = new List<Identifier>() { new Identifier() { Value = property.Key } }
                            }
                    };
                    columnK.Add(key);
                }
                var row = new List<WRowValue>() { new WRowValue() { ColumnValues = columnV } };
                var source = new WValuesInsertSource() { RowValues = row };
                var target = new WNamedTableReference() { TableObjectString = "Node" };
                var InsertStatement = new WInsertSpecification()
                {
                    Columns = columnK,
                    InsertSource = source,
                    Target = target
                };
                var InsertNode = new WInsertNodeSpecification(InsertStatement);
                SqlTree = InsertNode;
                return SqlTree;
            }

            // If needed to add edge, consturct new InsertEdgeStatement

            if (context.AddEMark)
            {
                var columnV = new List<WScalarExpression>();
                var columnK = new List<WColumnReferenceExpression>();

                foreach (var property in context.Properties)
                {
                    var value = new WValueExpression(property.Value, true);
                    columnV.Add(value);
                    var key = new WColumnReferenceExpression()
                    {
                        MultiPartIdentifier =
                            new WMultiPartIdentifier()
                            {
                                Identifiers = new List<Identifier>() { new Identifier() { Value = property.Key } }
                            }
                    };
                    SelectBlock.SelectElements.Add(new WSelectScalarExpression() { SelectExpr = new WValueExpression(property.Value, true) });
                    columnK.Add(key);
                }
                var row = new List<WRowValue>() { new WRowValue() { ColumnValues = columnV } };
                var source = new WValuesInsertSource() { RowValues = row };
                var target = new WNamedTableReference() { TableObjectString = "Edge" };
                var InsertStatement = new WInsertSpecification()
                {
                    Columns = columnK,
                    InsertSource = new WSelectInsertSource() { Select = SelectBlock },
                    Target = target
                };
                var InsertEdge = new WInsertEdgeSpecification(InsertStatement) { SelectInsertSource = new WSelectInsertSource() { Select = SelectBlock } };
                SqlTree = InsertEdge;
                return SqlTree;
            }

            // If needed to remove node/edge, construct new deleteEdge/Node Specification
            if (context.RemoveMark)
            {
                if (context.PrimaryInternalAlias[0].IndexOf("N_") != -1)
                {
                    var TargetClause = new WNamedTableReference()
                    {
                        TableObjectName =
                            new WSchemaObjectName()
                            {
                                Identifiers = new List<Identifier>() {new Identifier() {Value = "Node"}}
                            },
                        TableObjectString = "Node",
                        Alias = new Identifier() {Value = context.PrimaryInternalAlias.First()}
                    };
                    var DeleteNodeSp = new WDeleteSpecification()
                    {
                       WhereClause = NewWhereClause,
                       FromClause = NewFromClause,
                       Target = TargetClause
                    };
                    SqlTree = DeleteNodeSp;
                    return SqlTree;
                }
                if (context.PrimaryInternalAlias[0].IndexOf("E_") != -1)
                {
                    var EC = new WEdgeColumnReferenceExpression()
                    {
                        Alias = context.PrimaryInternalAlias.First(),
                        MultiPartIdentifier =
                            new WMultiPartIdentifier()
                            {
                                Identifiers = new List<Identifier>() {new Identifier() {Value = "Edge"}}
                            }
                    };
                    var DeleteEdgeSp = new WDeleteEdgeSpecification(SelectBlock);
                    SqlTree = DeleteEdgeSp;
                    return SqlTree;
                }
            }
            return SqlTree;
        }

    }
}




