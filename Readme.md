# Infered

Console application for infering probabilistic models parameters. Simple.

## Motivation

There are a set of beautiful tools for probabilistic inference, such as
PyMC3, Stan, Pyro, Tensorflow Probability, but first two requires some build tools to run,
second two are unavailable for me.

Since it's hard to distribute binary without build tools, it's possible with:
1. Infer.Net - for inference,
2. Pidgin - for text parsing,
3. CSVHelper - for CSV parsing.

## Build and run
Use `dotnet-cli`.

```
dotnet restore
dotnet publish --runtime your_runtime
```

`your_runtime` - win10-x64, ubuntu-x64 and so on.

To run infering pass the command line parameters:

```
infered data.csv model_definition.model parameter_name_to_infer_1 ... parameter_name_to_infer_N
```

## CSV input
All data to be used, must be numerical. No boolean or strings. Real only.

## Model syntax
All interpreted lines should have the following syntax:

```
parameter_name ~ expression
```

Other text will be ignored.

`expression`, is one of the following:

1. Random Variable Definition (details below),
2. Mathematical expression: +, -, *, /, ^ (power),
3. All numbers should be formatted as with floating point: #.#, even if they are integers!
4. Every expression can use *only **defined previously** parameters* or 
*values from CSV data* (by column names),
5. Function call: Exp, Log, Logistic, Max, Min. Some inference cases on functions are problem. It's better to transform all your input before inference.

Random variables:
1. GaussianFromMeanAndVariance(mean, variance),
2. GammaFromShapeAndScale(shape, scale),
3. BetaFromMeanAndVariance(mean, variance).

[Details](https://dotnet.github.io/infer/userguide/Double%20factors.html).

## Example
In the repository you may find a sample:

```
Any string without tilde are not interpreted.
You may write anything.

mean1 ~ GaussianFromMeanAndVariance(1.0, 0.1)
sd1 ~ GammaFromMeanAndVariance(1.0, 1.0)

mean2 ~ GaussianFromMeanAndVariance(1.0, 0.1)
sd2 ~ GammaFromMeanAndVariance(1.0, 1.0)

alpha ~ GaussianFromMeanAndVariance(mean1, sd1)
beta ~ GaussianFromMeanAndVariance(mean2, sd2)

Define target
Y ~ Exp(GaussianFromMeanAndVariance(alpha * X1 + beta * X2, 0.01))
```

Run with command:

```
dotnet run example/example.csv example/example.model alpha beta
```

The output will be:

```
Compiling model...done.
Iterating: 
.........|.........|.........|.........|.........| 50
alpha: Gaussian(2.011, 7.463e-05)
beta: Gaussian(2.974, 0.00043)
```

![Fit chart](images/fit_chart.png?raw=1 "Fit chart")

## License
WTFPL.
