import { defineConfig } from 'astro/config';
import { siteConfig } from './site.config.mjs';

export default defineConfig({
  site: siteConfig.siteUrl,
  base: siteConfig.basePath,
  output: 'static',
  trailingSlash: 'always',
});
