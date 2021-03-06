module.exports = function(grunt) {
  require('load-grunt-tasks')(grunt);
  var path = require('path');
  var meta = grunt.file.readJSON('config/meta.json');

  grunt.initConfig({
    pkg: grunt.file.readJSON('package.json'),
    pkgMeta: meta,
    dest: grunt.option('target') || 'dist',
    basePath: path.join('<%= dest %>', 'App_Plugins', CleanName(meta.name)),

    watch: {
      options: {
        spawn: false,
        atBegin: true
      },

      app_plugins: {
        files: ['./**/**'],
        tasks: ['copy:app_plugins']
      },

      dll: {
        files: ['./**/*.cs'],
        tasks: ['msbuild:dist', 'copy:dll']
      }
    },

    copy: {
      app_plugins: {
        cwd: './App_Plugins/UserGroupPermissions',
        src: ['**'],
        dest: '<%= basePath %>',
        expand: true
      },
      dll: {
        cwd: './bin/',
        src: 'UserGroupPermissions.dll',
        dest: '<%= dest %>/bin/',
        expand: true
      },

      nuget: {
        files: [
          {
            cwd: '<%= dest %>/App_Plugins',
            src: ['**/*', '!bin', '!bin/*'],
            dest: 'tmp/nuget/content/App_Plugins',
            expand: true
          },
          {
            cwd: '<%= dest %>/UserGroupPermissions/',
            src: ['**/*'],
            dest: 'tmp/nuget/content/UserGroupPermissions',
            expand: true
          },
          {
            cwd: '<%= dest %>/bin',
            src: ['*.dll'],
            dest: 'tmp/nuget/lib/net40',
            expand: true
          }
        ]
      },
      umbraco: {
        cwd: '<%= dest %>',
        src: '**/*',
        dest: 'tmp/umbraco',
        expand: true
      }
    },

    nugetpack: {
        dist: {
            src: 'tmp/nuget/package.nuspec',
            dest: 'pkg'
        }
    },

    template: {
        'nuspec': {
            'options': {
                'data': { 
                    name: '<%= pkgMeta.name %>',
                    version: '<%= pkgMeta.version %>',
                    url: '<%= pkgMeta.url %>',
                    license: '<%= pkgMeta.license %>',
                    licenseUrl: '<%= pkgMeta.licenseUrl %>',
                    author: '<%= pkgMeta.author %>',
                    authorUrl: '<%= pkgMeta.authorUrl %>',
                    files: [{ path: 'tmp/nuget/content/App_Plugins', target: 'content/App_Plugins'}]
                }
            },
            'files': { 
                'tmp/nuget/package.nuspec': ['config/package.nuspec']
            }
        }
    },

    umbracoPackage: {
      main: {
        src: 'tmp/umbraco',
        dest: 'pkg',
        options: {
          name: '<%= pkgMeta.name %>',
          version: '<%= pkgMeta.version %>',
          url: '<%= pkgMeta.url %>',
          license: '<%= pkgMeta.license %>',
          licenseUrl: '<%= pkgMeta.licenseUrl %>',
          author: '<%= pkgMeta.author %>',
          authorUrl: '<%= pkgMeta.authorUrl %>',
          manifest: 'config/package.xml',
          readme: grunt.file.read('config/readme.txt'),
          outputName: 'UserGroupPermissions_<%= pkgMeta.version %>.zip'
        }
      }
    },

    clean: {
      build: '<%= grunt.config("basePath").substring(0, 4) == "dist" ? "dist/**/*" : "null" %>',
      tmp: ['tmp']
    },

    assemblyinfo: {
      options: {
        files: ['UserGroupPermissions.csproj'],
        filename: 'AssemblyInfo.cs',
        info: {
          version: '<%= (pkgMeta.version.indexOf("-") >= 0 ? pkgMeta.version.substring(0, pkgMeta.version.indexOf("-")) : pkgMeta.version) %>',
          fileVersion: '<%= pkgMeta.version %>'
        }
      }
    },

    msbuild: {
      options: {
        stdout: true,
        verbosity: 'quiet',
        maxCpuCount: 4,
        version: 4.0,
        buildParameters: {
          WarningLevel: 2,
          NoWarn: 1607
        }
      },
      dist: {
        src: ['UserGroupPermissions.csproj'],
        options: {
          projectConfiguration: 'Release',
          targets: ['Clean', 'Rebuild']
        }
      }
    }
  });

  grunt.registerTask('default', ['clean', 'assemblyinfo', 'msbuild:dist', 'copy:dll', 'copy:app_plugins']);
  grunt.registerTask('nuget',   ['clean:tmp', 'default', 'copy:nuget', 'template:nuspec', 'nugetpack']);
  grunt.registerTask('umbraco', ['clean:tmp', 'default', 'copy:umbraco', 'umbracoPackage:main']);
  grunt.registerTask('package', ['clean:tmp', 'default', 'copy:nuget', 'template:nuspec', 'nugetpack', 'copy:umbraco', 'umbracoPackage:main', 'clean:tmp']);
};

// Cleans a name (e.g., removes spaces).
function CleanName(name) {
    return name.replace(/ /g, "");
}