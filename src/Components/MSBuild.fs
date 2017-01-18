namespace Ionide.VSCode.FSharp

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.vscode
open Fable.Import.Node

open DTO
open Ionide.VSCode.Helpers

module MSBuild = 
    let private outputChannel = window.createOutputChannel "msbuild"
    let private logger = ConsoleAndOutputChannelLogger(Some "msbuild", Level.DEBUG, Some outputChannel, Some Level.DEBUG)

    let msbuildLocation = 
        match Environment.msbuild with
        | Some location -> location
        | None -> Configuration.get "msbuild.exe" "FSharp.msbuildLocation"

    let invokeMSBuild project =
        let safeproject = sprintf "\"%s\"" project
        logger.Debug("invoking msbuild from %s on %s", msbuildLocation, safeproject)
        Process.spawnWithNotification msbuildLocation "mono" safeproject outputChannel |> ignore

    /// discovers the project that the active document belongs to and builds that
    let buildCurrentProject () = 
        logger.Debug("discovering project")
        match window.activeTextEditor.document with
        | Document.FSharp | Document.CSharp | Document.VB ->
            let currentProject = Project.getLoaded () |> Seq.where (fun p -> p.Files |> List.exists (String.endWith window.activeTextEditor.document.fileName)) |> Seq.tryHead
            match currentProject with
            | Some p -> 
                logger.Debug("found project %s", p.Project)
                invokeMSBuild p.Project
            | None -> 
                logger.Debug("could not find a project that contained the file %s", window.activeTextEditor.document.fileName)
                ()
        | Document.Other -> logger.Debug("I don't know how to handle a project of type %s", window.activeTextEditor.document.languageId)
        
    /// prompts the user to choose a project and builds that project
    let buildProject () =
        promise {
            logger.Debug "building current project"
            let projects = Project.getAll () |> ResizeArray
            if projects.Count <> 0 then
                let opts = createEmpty<QuickPickOptions>
                opts.placeHolder <- Some "Project to build"
                let! chosen = window.showQuickPick(projects |> Case1, opts)
                logger.Debug("user chose project %s", chosen)
                if JS.isDefined chosen 
                then 
                    invokeMSBuild chosen
        }
        
    let activate disposables = 
        let registerCommand com (action : unit -> _) = vscode.commands.registerCommand(com, unbox<Func<obj, obj>> action) |> ignore
        logger.Debug("MSBuild found at %s", msbuildLocation)
        registerCommand "MSBuild.buildCurrent" buildCurrentProject
        registerCommand "MSBuild.buildSelected" buildProject
        logger.Debug("MSBuild activated")
        ()