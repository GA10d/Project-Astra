(function(root){
  'use strict';
  const types=[{description:'星系配置 JSON',accept:{'application/json':['.json']}}];
  async function read(handle) {
    const file=await handle.getFile();
    if(file.size>2*1024*1024)throw Error('文件超过 2 MB');
    return {file,text:await file.text()};
  }
  async function write(binding,text) {
    const current=await read(binding.handle);
    if(current.text!==binding.expectedText)throw Error('原文件已被其他程序修改，未覆盖。请另存为保留当前编辑，或重新打开原文件合并。');
    let stream;
    try {
      stream=await binding.handle.createWritable();
      await stream.write(text);await stream.close();
    } catch(error) {
      if(stream)try{await stream.abort();}catch(_){}
      throw error;
    }
    binding.expectedText=text;
    return binding;
  }
  const api={types,read,write};
  if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.NavEditorFileStore=api;
})(typeof window==='undefined'?globalThis:window);
