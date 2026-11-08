# Omny CMS

Omny CMS follows the Omny philosophy "Everything you need, the way you want it.".  There are several different ways you might want to use Omny CMS.

## Use cases

### Technical content management

Omny’s content builder is both a .NET tool and a library. It can be customized via plugins and custom JSON manifest to configure default tools and custom plugins. It is best used with setting up build systems with previews on branches integrated via tools like CloudFlare Workers or Google Firebase Hosting. GitHub pages or other static hosting, like using AWS S3 buckets with Cloudfront are also practical.

#### Supporting less technical users

Using Omny’s content builder over existing tools like [Jekyll](https://jekyllrb.com/) or [Hexo](https://hexo.io/) probably isn’t something worth doing on its own unless you need a high degree of customization. Where it gets useful is in providing the advantages of a strong technical setup with having convenient editing for less technical users. Abstracting away the complexity into a simple to use system for specific uses is what Omny is all about.

The field and type system allows significant configuration with just a JSON manifest. Combined with plugins for the builder, it can be used to quickly define a simple interface for editing custom information and compile that as desired into the final content.

Plugins can also be made to make it so the editing interface makes it easy to read and write content types for other use cases. Omny has a Hexo plugin for reading and writing blog posts. It uses a separate, appropriate field for each portion of the front matter and of course the primary content. Along with the ability to trigger and monitor builds, this means we can have a non-technical user create and edit blog posts and publish them to a preview site without knowing anything about Hexo. They can also be given the ability to deploy the changes to the live site from a button in the editor if they have permission to merge. 

### Flexibile site building

The editor can be deployed alongside a set of plugins such that each site can use any subset of the plugins and configure them. This still needs some development, but can be used to some degree with APIs, personal access tokens saved into local storage or appsettings.json with some other configuration to control access currently. This will be supplemented by making the ability to deploy from APIs without git backing and use of a GitHub app to support different approaches in the future.

### Advanced site / content management

Omny CMS was designed with more complex scenarios in mind. It can easily be adapted to work with multiple repos for large organizations with multiple departments and users with varying levels of permission or needing approval for certain changes.

It can also be used to manage content that isn’t for websites and can transform that into useful outputs.
