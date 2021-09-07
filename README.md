<!--
Based on the README.md template found at https://github.com/othneildrew/Best-README-Template/blob/master/README.md
-->

[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]
[![Build Status][build-sheld]][build-url]


<br />

<!-- TABLE OF CONTENTS -->
<details open="open">
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#prerequisites">Prerequisites</a></li>
        <li><a href="#installation">Installation</a></li>
      </ul>
    </li>
    <li><a href="#roadmap">Roadmap</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
    <li><a href="#contact">Contact</a></li>
    <li><a href="#acknowledgements">Acknowledgements</a></li>
  </ol>
</details>



<!-- ABOUT THE PROJECT -->
## About The Project

BlazeBin is a pastebin service that was created to address several shortcomings in current offerings. These range from limited UIs, a bad editing experience, only being able to post a single file at a time, etc. BlazeBin addresses these by leveraging [monaco-editor](https://github.com/microsoft/monaco-editor) to handle syntax hilighting and editing. BlazeBin supports bundling multiple files together to aid in context when sharing issues. BlazeBin is privacy and safety focused! It will never display ads or collect user data for any purpose other than to monitor and improve the site. Enabling your browser's Do Not Track header is all that's required to opt-out of this simple telemetry! (see [blazebin.io's privacy docs](https://github.com/BlazeBin/policy/blob/main/privacy.md) for more detial.


<!-- GETTING STARTED -->
## Getting Started

Clone and open the project in your favorite IDE/Editor!

### Prerequisites

For development:
1. Install Visual Studio Code, Visual Studio 2019+, or Rider
1. Install the dotnet 6 preview 7 or higher SDK

For self-hosting:
1. (Optional) Install Docker
1. (Optional) Install docker-compose
1. (Optional) An Azure subscription (to leverage Azure KeyVault and Application Insights)


### Installation

The best and easiest way to host BlazeBin yourself is to run the docker container. This container does not handle SSL, so you will want to front it with nginx either in another container, or as a standalone server.

Command line:
Note: as an alternative to mounting the appsettings.json file, you can provide a .env file with your setting overrides. the env var name will be of the format `BLAZEBIN_[SECTION]__[SETTING]=value`. Example: `BLAZEBIN_GROOMING__ENABLED=true`

```sh
docker run -d -v ~/appsettings.json:/app/appsettings.production.json -v ~/data:/app/data --memory 500M --cpus 2 -p 80:80 -e ASPNETCORE_URLS=http://+:80 ghcr.io/cisien/blazebin:latest
```

Docker Compose:
```yml
version: '3.7'
services:
  repl:
    image: ghcr.io/cisien/blazebin:latest
    restart: always
    environment: 
      - ASPNETCORE_URLS=http://+:80
    ports:
      - '80:80'
    volumes:
      - data:/app/data
volumes:
  - data
```

<!-- ROADMAP -->
## Roadmap

See the [open issues](https://github.com/Cisine/BlazeBin/issues) for a list of proposed features (and known issues).

<!-- CONTRIBUTING -->
## Contributing

Contributions are what make the open source community such an amazing place to be learn, inspire, and create. Any contributions you make are **greatly appreciated**.

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

<!-- LICENSE -->
## License

Distributed under the MIT License. See `LICENSE` for more information.

<!-- CONTACT -->
## Contact

Chris Curwick - [@Cisien on Discord](https://discord.gg/csharp)<br />
Project Link: [https://github.com/Cisien/BlazeBin](https://github.com/Cisien/BlazeBin)<br />
Policy Documents: [https://github.com/BlazeBin/policy](https://github.com/BlazeBin/policy)

<!-- ACKNOWLEDGEMENTS -->
## Acknowledgements
* [dotnet](https://github.com/dotnet)
* [monaco-editor](https://github.com/microsoft/monaco-editor)
* [BlazorMonaco](https://github.com/serdarciplak/BlazorMonaco)
* [vscode-icons](https://github.com/vscode-icons/vscode-icons)
* [Img Shields](https://shields.io)

<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->
[contributors-shield]: https://img.shields.io/github/contributors/Cisien/BlazeBin.svg?style=for-the-badge
[contributors-url]: https://github.com/Cisien/BlazeBin/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/Cisien/BlazeBin.svg?style=for-the-badge
[forks-url]: https://github.com/Cisien/BlazeBin/network/members
[stars-shield]: https://img.shields.io/github/stars/Cisien/BlazeBin.svg?style=for-the-badge
[stars-url]: https://github.com/Cisien/BlazeBin/stargazers
[issues-shield]: https://img.shields.io/github/issues/Cisien/BlazeBin.svg?style=for-the-badge
[issues-url]: https://github.com/Cisien/BlazeBin/issues
[license-shield]: https://img.shields.io/github/license/Cisien/BlazeBin.svg?style=for-the-badge
[license-url]: https://github.com/Cisien/BlazeBin/blob/master/LICENSE
[build-sheld]: https://img.shields.io/github/workflow/status/Cisien/BlazeBin/build/main?style=for-the-badge
[build-url]: https://github.com/Cisien/BlazeBin/actions/workflows/main.yml
