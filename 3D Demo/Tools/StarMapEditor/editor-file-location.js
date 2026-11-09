'use strict';

const EditorFileLocation = (() => {
  const $ = id => document.getElementById(id);
  const describe = file => file.path ? file.path + (file.verifiedPath ? '' : '（手动标注）') : file.name + '（浏览器未提供完整路径）';
  function edit(file, update) {
    const entered = prompt('粘贴此文件的完整路径。留空可清除标注。路径标注只用于显示，写入位置由文件选择器决定。', file.path || '');
    if (entered === null) return;
    const path = entered.trim().replace(/^"(.*)"$/, '$1');
    const windows = /^(?:[A-Za-z]:[\\/]|\\\\)/.test(path);
    const filename = path.split(/[\\/]/).pop();
    if (path && ((!windows && !path.startsWith('/')) ||
      (windows ? filename.toLowerCase() !== file.name.toLowerCase() : filename !== file.name))) {
      alert('请粘贴以「' + file.name + '」结尾的完整文件路径。');
      return;
    }
    update({...file, path});
  }
  function render(current, saved, update, busy = false) {
    $('current-config-path').textContent = current ? describe(current) : '尚未打开配置';
    $('last-save-path').textContent = saved ? describe(saved) : '尚无保存记录';
    $('last-save-time').textContent = saved ? new Date(saved.at).toLocaleString('zh-CN', {
      year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',second:'2-digit',hour12:false
    }) : '—';
    for (const [id, file] of [['label-current-path', current], ['label-saved-path', saved]]) {
      const button = $(id);
      button.hidden = !file || file.verifiedPath===true;
      button.disabled = busy;
      button.onclick = file ? () => edit(file, update) : null;
    }
  }
  return { render, newFile: (name,handle) => ({name, id: globalThis.crypto?.randomUUID?.() ?? Date.now()+'-'+Math.random(), path:handle?.path || '', verifiedPath:!!handle?.path}) };
})();
