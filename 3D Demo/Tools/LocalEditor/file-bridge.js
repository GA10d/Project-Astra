'use strict';

window.EditorLocalFiles = (() => {
  const editor = location.pathname.split('/')[1];
  async function request(operation, data) {
    let response;
    try {
      response = await fetch('/api/' + operation, {
        method:'POST',headers:{'Content-Type':'application/json','X-Astra-Editor':'1'},
        body:JSON.stringify(data)
      });
    } catch {
      throw Error('本地启动器未连接。请重新运行 .cmd。当前浏览器草稿仍可下载。');
    }
    const result = await response.json();
    if (!response.ok) throw Error(result.error || '本地文件操作失败。');
    return result;
  }

  class LocalFile {
    constructor({id, path, name, version}) {
      Object.assign(this,{id,path,name,version});
    }
    async getFile() {
      const result=await request('read',{id:this.id});
      this.version=result.version;
      return new File([result.text],this.name,{type:'application/json',lastModified:result.modified});
    }
    async isSameEntry(other) { return other instanceof LocalFile && this.path.toLowerCase()===other.path.toLowerCase(); }
    async queryPermission() {
      try { await request('read',{id:this.id}); return 'granted'; }
      catch { return 'denied'; }
    }
    requestPermission() { return this.queryPermission(); }
    async createWritable() {
      const version=this.version;
      let text;
      return {
        write: async value=>{text=value;},
        close: async ()=>{this.version=(await request('write',{id:this.id,version,text})).version;},
        abort: async ()=>{text=undefined;}
      };
    }
  }

  async function pick(action, name) {
    const result=await request('pick',{action,name,editor});
    if(result.cancelled)throw new DOMException('已取消文件选择。','AbortError');
    return new LocalFile(result);
  }
  return {
    open:async()=>[await pick('open','')],
    save:options=>pick('save',options.suggestedName),
    store:handle=>handle instanceof LocalFile ? {...handle, astraLocal:true} : handle,
    restore:record=>record?.astraLocal ? new LocalFile(record) : record
  };
})();
