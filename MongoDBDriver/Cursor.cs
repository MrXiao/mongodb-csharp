using System;
using System.Collections.Generic;
using System.IO;

using MongoDB.Driver.Bson;
using MongoDB.Driver.IO;

namespace MongoDB.Driver
{
    
	public class Cursor : ICursor {
        private Connection connection;
        
        private long id = -1;
        public long Id{
            get {return id;}
        }       
        
        private String fullCollectionName;
        public string FullCollectionName {
            get {return fullCollectionName;}
        }

        private Document spec;
        public ICursor Spec (Document spec){
            TryModify();
            this.spec = spec;
            return this;
        }
        
        private int limit;
        public ICursor Limit (int limit){
            TryModify();
            this.limit = limit;
            return this;
        }
        
        private int skip;        
        public ICursor Skip (int skip){
            TryModify();
            this.skip = skip;
            return this;
        }
        
        private Document fields;        
        public ICursor Fields (Document fields){
            TryModify();
            this.fields = fields;
            return this;
        }
        
        
        private bool modifiable = true;
        public bool Modifiable{
            get {return modifiable;}
        }
        
        private ReplyMessage reply;
        
        public Cursor(Connection conn, string fullCollectionName){
            this.connection = conn;
            this.fullCollectionName = fullCollectionName;
        }
        
        public Cursor(Connection conn, String fullCollectionName, Document spec, int limit, int skip, Document fields):
                this(conn,fullCollectionName){
            if(spec == null)spec = new Document();
            this.spec = spec;
            this.limit = limit;
            this.skip = skip;
            this.fields = fields;
        }
        
        public IEnumerable<Document> Documents{
            get{
                if(this.reply == null){
                    RetrieveData();
                }
                int docsReturned = 0;
                Document[] docs = this.reply.Documents;
                Boolean shouldBreak = false;
                while(!shouldBreak){
                    foreach(Document doc in docs){
                        if((this.limit == 0) || (this.limit != 0 && docsReturned < this.limit)){
                            docsReturned++;
                            yield return doc;
                        }else{
                            shouldBreak = true;
                            yield break;
                        }
                    }
                    if(this.Id != 0 && shouldBreak == false){
                        RetrieveMoreData();                 
                        docs = this.reply.Documents;
                        if(docs == null){
                            shouldBreak = true; 
                        }
                    }else{
                        shouldBreak = true;
                    }
                }
            }           
        }
        
        private void RetrieveData(){
            QueryMessage query = new QueryMessage();
            query.FullCollectionName = this.FullCollectionName;
            query.Query = this.spec;
            query.NumberToReturn = this.limit;
            query.NumberToSkip = this.skip;
            if(this.fields != null){
                query.ReturnFieldSelector = this.fields;
            }
            try{
                this.reply = connection.SendTwoWayMessage(query);
                this.id = this.reply.CursorID;
                if(this.limit < 0)this.limit = this.limit * -1;
            }catch(IOException ioe){
                throw new MongoCommException("Could not read data, communication failure", this.connection,ioe);
            }

        }
        
        private void RetrieveMoreData(){
            GetMoreMessage gmm = new GetMoreMessage(this.fullCollectionName, this.Id, this.limit);
            try{
                this.reply = connection.SendTwoWayMessage(gmm);
                this.id = this.reply.CursorID;
            }catch(IOException ioe){
                this.id = 0;
                throw new MongoCommException("Could not read data, communication failure", this.connection,ioe);
            }
        }
        
        
        public void Dispose(){
            if(this.Id == 0) return; //All server side resources disposed of.
            KillCursorsMessage kcm = new KillCursorsMessage(this.Id);
            try{
                this.id = 0;
                connection.SendMessage(kcm);
            }catch(IOException ioe){
                throw new MongoCommException("Could not read data, communication failure", this.connection,ioe);
            }
        }
        
        private void TryModify(){
            if(this.modifiable) return;
            throw new InvalidOperationException("Cannot modify a cursor that has already returned documents.");
        }
    }
}
