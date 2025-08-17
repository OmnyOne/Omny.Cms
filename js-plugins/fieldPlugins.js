export const fieldPlugins = {
  registry: {},
  register(name, plugin) {
    this.registry[name] = plugin;
  },
  init(name, elementId, value, dotnetRef) {
    const plugin = this.registry[name];
    if (plugin && plugin.init) {
      const element = document.getElementById(elementId);
      plugin.init(element, value, v => dotnetRef.invokeMethodAsync('OnValueChanged', v));
    }
  },
  destroy(name, elementId) {
    const plugin = this.registry[name];
    if (plugin && plugin.destroy) {
      const element = document.getElementById(elementId);
      plugin.destroy(element);
    }
  }
};

import { register as registerToast } from './toastMarkdownPlugin.js';
registerToast(fieldPlugins);

window.fieldPlugins = fieldPlugins;
