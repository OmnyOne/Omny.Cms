import { Editor } from '@toast-ui/editor';
import '@toast-ui/editor/dist/toastui-editor.css';

export function register(fieldPlugins) {
  fieldPlugins.register('toast-markdown', {
    init(element, value, onChange) {
      const editor = new Editor({
        el: element,
        height: '400px',
        initialEditType: 'markdown',
        previewStyle: 'vertical',
        initialValue: value || ''
      });
      editor.on('change', () => {
        onChange(editor.getMarkdown());
      });
      element.__toastEditor = editor;
    },
    destroy(element) {
      const editor = element.__toastEditor;
      if (editor) {
        editor.destroy();
        delete element.__toastEditor;
      }
    }
  });
}
