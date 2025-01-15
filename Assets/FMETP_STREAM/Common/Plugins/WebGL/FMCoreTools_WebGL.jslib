mergeInto(LibraryManager.library, {
    //Unity 2021_2 or before: Pointer_stringify(_src);
    //Unity 2021_2 or above: UTF8ToString(_src);
    // FMCoreTools_WebGLAddScript_2021_2: function (_innerHTML, _src)
    // {
    //   var script = UTF8ToString(_innerHTML);
    //   var src = UTF8ToString(_src);
    //   var scriptElement = document.createElement("script");
    //   scriptElement.innerHTML = script;
    //   if (src.length > 0) scriptElement.setAttribute("src", src);
    //   document.head.appendChild(scriptElement);
    // },
    // FMCoreTools_WebGLAddScript_2021_2: function (_innerHTML, _src, _id)
    // {
    //   var script = UTF8ToString(_innerHTML);
    //   var src = UTF8ToString(_src);
    //   var id = UTF8ToString(_id);
    //   var scriptElement = document.createElement("script");
    //   scriptElement.innerHTML = script;
    //   if (src.length > 0) scriptElement.setAttribute("src", src);
    //   if (id.length > 0) scriptElement.setAttribute("id", id);
    //   document.head.appendChild(scriptElement);
    // },

    FMCoreTools_WebGLAddScript_2021_2: function (_innerHTML, _src, _id, _onload)
    {
      var script = UTF8ToString(_innerHTML);
      var src = UTF8ToString(_src);
      var id = UTF8ToString(_id);
      var onload = UTF8ToString(_onload);
      var scriptElement = document.createElement("script");
      scriptElement.innerHTML = script;
      if (src.length > 0) scriptElement.setAttribute("src", src);
      if (id.length > 0) scriptElement.setAttribute("id", id);
      if (onload.length > 0) scriptElement.setAttribute("onload", onload);
      document.head.appendChild(scriptElement);
    },
});
