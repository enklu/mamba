# Mamba

[![CodeFactor](https://www.codefactor.io/repository/github/enklu/mamba/badge)](https://www.codefactor.io/repository/github/enklu/mamba) [![Join the chat at https://gitter.im/enklu/mamba](https://badges.gitter.im/enklu/mamba.svg)](https://gitter.im/enklu/mamba?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

*Why is this named mamba?* My friend, the Black Mamba can see infrared-- JUST LIKE A KINECT! Get it? Did you get it...?


### Development Setup

* .NET Framework 4.7.1.
* Kinect v2.

Simply open the solution and build. Enklu Nuget packages will be resolved from public Nuget feed.

### Configuration

Mamba is configured in two places:

1. By the `app-config.json`. This file allows users to configure the environment and experience details. Look in `app-config.example.json` for an example.
2. In the experience, via the [Enklu Editor](https://cloud.enklu.com). Here, create a Kinect element. You can read more [here](https://enklu.helpdocs.io/article/787k2gtm13-kinect-integration).

### Deployment

See Mamba [releases](https://github.com/enklu/mamba/releases/) for pre-built Windows binaries.
